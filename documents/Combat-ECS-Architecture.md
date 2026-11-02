# 战斗 ECS 架构说明

本文按当前 `Client/Assets/GameMain/CombatDots` 代码整理。`Zizouqi.CombatDots` 是独立 asmdef，引用 Entities、Burst、Collections、Mathematics；热更层通过 `*Bridge` 访问战斗数据。这里的“组件”指 `IComponentData`，“缓冲区”指 `IBufferElementData`。把数据放入 Entity 并不意味着访问它的代码自动并行。

## 一帧战斗如何运行

```text
GameState_Battle.OnUpdate
  └─ 消耗固定逻辑步（当前 30 Hz，每渲染帧最多补 2 步）
      └─ HeroComponent.OnLogicUpdate
          └─ CombatFrameScheduler.AdvanceBattle
              ├─ 单位快照并安排最近目标 Job
              ├─ 目标与技能意图 Job 完成后，推进冷却、Buff、攻击施法计时
              ├─ 全部单位预选普通/自动技能，写入意图 Buffer
              ├─ 移动单位的插值计算 Job，写入 Entity 位置与到达标记
              ├─ 单位状态 Job 按技能轮转顺序生成候选动作
              ├─ 依次提交单位施法、寻路占位和技能触发
              ├─ 子弹激活音频 Job，按子弹顺序在主线程播放音频
              ├─ 安排追踪/环绕/标记点与曲线子弹运动 Job、命中候选 Job，构建空间索引
              ├─ 等待 Job 完成，按生成顺序复核候选并处理命中
              ├─ 点位定时器 Job、按顺序更新点位及提交子弹意图
              ├─ 点位 OnActive 事件 Job 生成表现意图
              └─ 消费本步后段新建子弹的激活音频
  └─ HeroComponent.UpdateBattlePresentation
      └─ 消费角色朝向与动画请求、刷新角色位置和血条，处理点位特效、子弹外观和伤害数字
```

主入口在 `Scripts/Procedures/GameStates/GameState_Battle.cs`；调度器在 `CombatDots/CombatFrameScheduler.cs`。单位、子弹、点位的回调仍会进入 `HeroComponent` 和热更逻辑。调度器刻意保留单位分组（1 后 0）、组内 UID、子弹生成序号等顺序，因为技能命令可以即时修改后续命中结果。

子弹逻辑 Entity 与外观分别处理：只有预制体恰好含一个 MeshRenderer、一个材质且无粒子或拖尾时，外观才注册为 Entities Graphics 原型并实例化为 Entity。其余预制体从 GameObject 池取得，仍会出现在 Hierarchy；预热用的源 GameObject 也可能留在非活动池中。GameObject 子弹的位置和朝向已从逻辑步移动到 `UpdateBattlePresentation` 的子弹表现阶段，曲线切线也在该阶段从 Entity 路径数据读取。Mesh 子弹的外观资源 ID、位置、朝向目标及简单规则的激活音效位置直接从 Entity 获取；GameObject 回退仍要找到对应的托管表现对象。`Combat.Visual.GameObjectFallback` 与 `Combat.Visual.GameObjectFallbacks` 分别标记回退取对象的时间和累计次数，`Combat.Visual.EntitySpawn` / `Combat.Visual.EntityPlayback` 标记 Entity 外观创建。Hierarchy 中有子弹 GameObject 不代表对应的子弹逻辑不是 Entity。

匹配简单规则的子弹不保留托管 `OwnerTriggerList`；这类 `BulletBase` 在激活和每步更新时也跳过无意义的触发器 Profiler 样本。它仍是逐弹托管桥接对象，因此这项改动只减少桥接开销，不等于把子弹提交阶段完全迁入 ECS。

伤害结算现在把伤害数值和命中位置写入棋盘 Entity 的 `CombatDamageNumberEvent` Buffer；每个渲染帧由 `UpdateBattlePresentation` 按事件顺序消费。伤害数字本身仍是 WorldCanvas 下的 UGUI Text GameObject，数字的取池、设置 Text 和每帧移动都在主线程。数字对象池始终将对象留在 WorldCanvas 下，回收时停用，复用时启用，避免每次命中都在 Canvas 与禁用根节点之间重新设置父节点。`Combat.Presentation.DamageNumber.Request`、`Setup`、`Update` 可分别定位请求、预制体设置和逐帧动画的开销。ECS 事件缓冲区解除了伤害逻辑对 UGUI 的直接调用，但伤害和 Buff 的结算仍走托管提交，尚不能称为完整逻辑 ECS。

## System 与 Job

| 名称 | 类型 / 调用方式 | 职责 | 线程情况 |
| --- | --- | --- | --- |
| `CombatCooldownSystem` | `SystemBase`；`CombatCooldownBridge.Advance` 手动 `Update()` | 减少单位/技能冷却剩余秒数 | `ScheduleParallel()`，本步读取前 `Complete()` |
| `CombatBuffTimerSystem` | `SystemBase`；`CombatBuffTimerBridge.Advance` 手动 `Update()` | 减少启用的 Buff 剩余毫秒 | `ScheduleParallel()`，本步读取前 `Complete()` |
| `CombatAttackCastSystem` | `SystemBase`；`CombatAttackCastBridge.AdvanceAll` 手动 `Update()` | 并行推进单位施法时间，写 `CombatSkillPhaseEvent`；前摇到期时按技能的触发器 Entity 链接顺序写 `CombatSkillTriggerInvocation` | Burst `IJobChunk.ScheduleParallel()`，单位顺序消费前 `Complete()` |
| `CombatMovementSystem` | `SystemBase`；单位阶段前手动 `Update()` | 从同一逻辑步的移动快照并行计算单位插值位置和到达标记 | Burst `ScheduleParallel()`，状态提交前 `Complete()` |
| `CombatUnitBrainSystem` | `SystemBase`；移动推进后手动 `Update()` | 根据单位状态、攻击与移动标记，按技能轮转顺序筛选候选，并从棋盘快照并行计算追击下一格 | Burst `IJobChunk.ScheduleParallel()`，按单位顺序提交前 `Complete()`；棋盘变化时实时复核寻路 |
| `AdvancePointTimersJob` | `IJobChunk`，位于 `CombatConditionProgramBridge` | 批量推进定时触发条件，写入本步是否到期 | Burst `ScheduleParallel`，随后 `Complete()` |
| `CombatPointTimedTriggerSystem` | `SystemBase`；点位阶段手动 `Update()` | 推进点位定时创建子弹或播放特效/音效规则，写执行意图 | Burst `IJobChunk.ScheduleParallel`，消费前 `Complete()` |
| `CombatPointTriggerSystem` | `SystemBase`；点位阶段末手动 `Update()` | 处理点位 OnActive 特效与音效事件，写播放意图 | Burst `ScheduleParallel()`，渲染消费前 `Complete()` |
| `CombatProjectileActiveAudioSystem` | `SystemBase`；单位阶段后及点位阶段后手动 `Update()` | 标记符合简单规则的新子弹激活音效待播放 | Burst `IJobChunk.ScheduleParallel`，播放前 `Complete()`；音效 API 留在主线程 |
| `CalculateStepsJob` | `IJobParallelFor`，位于 `CombatBezierStepBatch` | 从单位快照批量计算普通及追踪目标的曲线子弹下一步位置/路径进度 | 满足至少 32 颗的门槛时才 Burst `Schedule`，随后 `Complete()`；目标状态失配则回退实时步进 |
| `MoveJob` | `IJobParallelFor`，位于 `CombatProjectileMotionBatch` | 从本步快照计算追踪、环绕和标记点子弹的下一步位置或生命周期 | 满足至少 32 颗非曲线子弹时 Burst `Schedule`；在触发器回调后核对原组件与目标快照，失配则回退实时步进 |
| `BuildSpatialIndexJob` | `IJobParallelFor`，位于 `CombatProjectileBroadphase` | 从单位快照建立网格空间索引 | Burst `Schedule`，随后 `Complete()` |
| `FindHitsJob` | `IJobParallelFor`，位于 `CombatProjectileHitCandidateBatch` | 为不追踪目标的曲线子弹按单位快照计算扫掠命中候选 | 至少 32 颗有效且不追踪目标的曲线子弹时 Burst `Schedule`；主线程按顺序复核 |
| `FindNearestJob` | `IJobParallelFor`，位于 `CombatUnitSkillPlanBatch` | 从本步单位快照为各单位预计算最近敌人 | 至少 16 个单位时 Burst `Schedule`，否则 `Run`；单位技能读取结果前完成 |
| `EvaluateSkillsJob` | `IJobParallelFor`，位于 `CombatUnitSkillPlanBatch` | 从同一快照计算每个普通技能的能量、旧目标、最近目标和范围检查结果 | 依赖 `FindNearestJob`；单位提交前完成 |
| `PublishIntentsJob` | `IJobChunk`，位于 `CombatUnitSkillPlanBatch` | 把普通技能计算结果直接写入各单位的 `CombatUnitSkillIntent` Buffer | 依赖 `EvaluateSkillsJob`；单位状态 Job 读取前完成；少于 16 单位时直接 `Run()`；显式调度，避免 `IJobEntity` 生成代码占位方法异常 |
| `SelectAndPrepareJob` / `FindAreaTargetsJob` | `IJobParallelFor`，位于 `CombatTriggerSimpleBatch` | 按单位快照选择直接、最近、持有者距离范围或攻击方向扇形内的目标，并准备单命令伤害数值 | 至少 16 条事件时 Burst 并行；范围目标按原规则排序后依序提交 |

上述手动驱动的 `SystemBase` 标有 `[DisableAutoCreation]`，不依赖 Unity 默认系统组自动更新。并行任务在本逻辑步使用结果前会完成；命中后的技能命令、GameObject 操作仍在主线程。`CombatFrameScheduler` 是静态调度类，不是 Entities 的 `SystemBase`。

三个子弹批处理对象（普通运动、曲线运动、曲线命中候选）复用托管容器；每步的 NativeArray 仍按 Job 生命周期创建和释放。快照只负责计算，触发器改变位置、目标或参数后会改用实时步进，避免把旧快照覆盖到当前 Entity。

子弹的生成序号名单现在保存在棋盘 Entity 的 `CombatProjectileRoster` Buffer。生成时按序登记，回收时把位置标为失效，下一逻辑步集中压缩。`CollectUpdateHandles` 不再每步复制全部子弹组件并排序；按子弹 ID / 施法者筛选也沿用同一有序名单。名单仍在主线程读取，以维持命中和命令提交顺序。

环绕弹现在另有 `CombatOrbitHitCandidateBatch`：当活跃环绕弹达到阈值时，Burst Job 并行用单位位置快照筛出半径和扫过角度范围内的候选目标，角度边界留有余量。主线程仍按子弹生成序号和目标 UID 顺序复核实时角度与半径并提交触发器。若同一步的单位位置、角度、角速度或环绕范围变化，候选快照失效并回退原扫描；少量环绕弹也直接走原路径，避免调度开销。

## IComponentData

| 组件 | 所属 Entity | 作用 |
| --- | --- | --- |
| `CombatBoard` | 战斗棋盘单例 | 战斗阶段、固定步累积时间、胜负待处理状态、棋盘偏移、UID/生成序号分配器、Buff tick 等全局状态 |
| `CombatUnit` | 每个棋子/单位 | UID、阵营、更新分组、存活状态、逻辑坐标、攻击目标、技能索引、部署格子 |
| `CombatUnitBrainState` / `CombatUnitBrainPlan` | 每个单位 | Idle/Moving/胜负等状态及本步是否可评估技能；不再创建 `Fsm<EntityQizi>` |
| `CombatMovement` | 正在移动的单位 | 起点、终点、经过时间；由 `CombatMovementBridge` 推进格子移动 |
| `CombatUnitAnimationRequest` / `CombatUnitFacingRequest` | 单位 | 本帧待消费的动画和朝向请求；最后一次请求生效，表现阶段读取并清空 |
| `CombatAttackCast` | 每个单位 | 当前技能 Entity、普攻技能序号、施法计时和各阶段的一次性标志 |
| `CombatSkillDefinition` / `CombatSkillRuntime` | 每个已装备技能 | 前者存编译后的技能参数，后者存运行时参数和冷却；当前旧技能对象仍参与施法回调 |
| `CombatTriggerDefinition` | 每个已编译的技能触发器 | 所属技能和单位、触发类型、原始顺序及启用状态；当前旧 `OneTrigger` 仍参与运行时提交 |
| `CombatCooldown` | 冷却计时 Entity | 剩余时间及施法、前摇、动画等参数；由冷却 System 推进 |
| `CombatBuffTimer` | 每个 Buff 计时 Entity | 时长、启用/激活/有效状态、层数相关参数、归属单位/子弹、Buff ID、光环顺序等 |
| `CombatPosPoint` | 每个战斗点位 | 句柄、生成顺序、位置、阵营、存活和持续时间、下次定时触发状态 |
| `CombatPosPointActive` | 点位 Entity | 可启用标记；关闭时实体保留在池中但不参与点位查询 |
| `CombatPointTimedTrigger` | 点位 Entity | 点位定时规则的间隔、累计时间、通过次数、命令数和命令种类 |
| `CombatProjectile` | 每颗子弹 | 句柄、生成顺序、子弹 ID、施放者 UID、位置、速度、目标、阵营、存活和生命周期 |
| `CombatProjectileActive` / `CombatMarkPointProjectile` | 子弹 Entity | 前者控制池中实体是否参与查询，后者标识标记点子弹的池类型 |
| `CombatProjectileSimpleHit` | 子弹 Entity | 可迁移的激活音效与命中触发器规则、伤害公式、音效播放序号；池复用时清空 |
| `CombatOrbitProjectile` | 环绕型子弹 | 环绕中心、角度、半径范围、角速度、终止角度 |
| `CombatBezierProjectile` | 曲线/穿透型子弹 | 路径起点、目标、控制点、已走距离、总距离、追踪目标标志 |
| `CombatTriggerExecutionState` | 条件程序 Entity | 递归调用深度及延迟释放状态，防止执行过程中销毁程序 |
| `CombatScheduledPointTimer` | 带定时条件的程序 Entity | 关联点位、定时节点索引、本步是否到期 |

一个子弹 Entity 可以同时拥有 `CombatProjectile` 和对应的专用子弹组件。四类子弹现在从统一的 `CombatProjectileSpawnRequest` 创建，简单命中规则也在请求中携带并在生成时写入 Entity，避免生成后反复通过 Bridge 写入基础组件。`BulletBase` 等托管对象仍负责触发器与 Unity 表现，并以句柄关联 Entity；这一步还没有消除每颗子弹的托管运行时对象。

完整移除 `BulletBase` 还需要一并替换四处运行时契约：`TargetPickerBullet` / `CombatTargetProjection` 把子弹当作 `EntityBase` 目标，`CombatCommandCallbacks` 对移除子弹及创建 Buff 调用托管对象，标记弹的 Buff 触发回调仍依赖托管对象，复杂伤害回退依赖来源 `Skill`。标记弹的 Buff 归属名单现已改为 Entity Buffer；只有回调对象映射还在托管侧。仅删除生成时的 `CreateBullet` 会让这些命令失去目标或跳过结算。新路径必须以子弹 Entity 句柄贯穿选目标、命令意图、Buff 归属和按序伤害结算，再把 GameObject 外观回退单独交给表现层。

选子弹现在可以将没有 `BulletBase` 投影对象的有效子弹 Entity 作为句柄目标传给命令程序；移除子弹命令按目标句柄查找现有托管对象并执行原销毁流程，找不到时可直接释放 Entity。创建 Buff、在子弹目标上执行其他命令以及通用触发器仍未完成句柄化，因此目前还没有启用无 `BulletBase` 的生成路径。

标记弹 Buff 的归属名单由 `CombatProjectileBuff` Buffer 保存。按子弹收集有效 Buff 时只遍历该子弹的计时器句柄，并保持 `ActiveOrder` 排序，避免每颗标记弹每步扫描全部 Buff 计时 Entity。Buff 的 `PerTick` / `OnDestory` 命令回调仍是托管逻辑。

单位 Buff 归属名单由 `CombatUnitBuffTimer` Buffer 保存，配置归属时登记，释放计时器时移除。护盾存在性检查、同 ID 堆叠、护盾结算及单位光环查找只遍历该单位的计时器，不再在每次命中或 Buff 操作时复制并扫描整个战场的计时 Entity。计时器的并行推进仍由 `CombatBuffTimerSystem` 完成；Buff 回调仍待迁移。

角色移动插值在 `CombatMovementSystem` 并行完成；技能预选先取本步快照，移动 Job 随后更新位置，`CombatUnitBrainSystem` 按单位状态与技能轮转顺序并行写候选动作和寻路下一格。旧角色 FSM 已移除；到达标记、施法和占位仍按单位顺序提交。若前面的单位使棋盘快照失效，提交时实时重新寻路，避免同一步的格子争用。角色 `OnLogicUpdate` 不再直接刷新血条或播放动画；移动朝向、动画 ID 写入单位 Entity 的表现请求，本渲染帧由 `UpdateBattlePresentation` 消费。角色位置同步到 GameObject 也在表现阶段，位置未变化时跳过地形采样。寻路回退、技能命令和伤害仍有主线程逻辑，因此不能把整个角色战斗链称为完全并行。

角色特效的 `AddSfx`、`RemoveSfx` 与每逻辑步的存活时间推进写入同一个单位 Entity Buffer。这样一帧含多个逻辑步时，表现阶段仍按事件原顺序重放刷新和到期。点位与子弹的表现请求先消费，然后重放角色特效；Unity 粒子和 GameObject 取池继续在主线程。Profiler 的 `Combat.Presentation.UnitSfx` 显示重放成本。此 Buffer 目前由主线程写入；后续若由 Job 产生特效事件，需要使用并行写入队列或 ECB 并按确定的逻辑顺序合并。

## IBufferElementData

| Buffer | 所属 Entity | 作用 |
| --- | --- | --- |
| `CombatBoardCell` | 棋盘单例 | 8×7 格子的占用 UID 与坐标 |
| `CombatProjectileVisualRequest` | 棋盘单例 | 待加载子弹外观的子弹句柄及生成序号；每渲染帧限量消费，跳过已销毁子弹 |
| `CombatDamageNumberEvent` | 棋盘单例 | 伤害数值与命中位置；逻辑阶段写入，表现阶段顺序消费并创建 UGUI 数字 |
| `CombatAttribute` | 单位 | 属性类型、基础值、附加值、百分比、上下限等；战斗属性的运行时存储 |
| `CombatSkillSlot` | 单位 | 普攻、自动、被动、特殊技能的槽位、技能 ID、威力、范围、目标类型 |
| `CombatUnitSkillIntent` | 单位 | 本逻辑步预计算的普通技能检查结果和目标 UID；状态 Job 按技能轮转顺序读取 |
| `CombatUnitBrainCandidate` | 单位 | 状态 Job 输出的施法、追击或等待候选；按单位顺序提交 |
| `CombatAutomaticSkillIntent` | 单位 | 本逻辑步冷却已就绪的自动技能槽索引；统一预选后在单位提交阶段施放 |
| `CombatUnitSfxEvent` | 单位 | 按发生顺序记录创建、移除、时间推进事件；表现阶段重放并操作特效 GameObject |
| `CombatTriggerListener` | 单位 | 按触发类型和注册 ID 索引触发器；托管 `TriggerList` 仍执行回调 |
| `CombatDeferredBuffRelease` | 单位 | 等当前触发结束后释放的 Buff 计时句柄 |
| `CombatProjectileHit` | 子弹 | 已命中单位 UID，用于穿透等子弹的重复命中控制 |
| `CombatProjectileBuff` | 标记弹 | 归属该子弹的 Buff 计时器句柄；生成、销毁和实体池复用时清空，托管回调目前仍通过计时器句柄映射到 Buff 对象 |
| `CombatPointTriggerEvent` / `CombatPointPresentationRule` / `CombatPointPresentationIntent` | 点位 | OnActive 事件、静态表现规则、特效或音效播放意图 |
| `CombatPointProjectileIntent` | 点位 | 定时到期后待顺序提交的子弹命令索引 |
| `CombatPointTimedPresentationRule` | 点位 | 定时到期后要播放的特效或音效配置 |
| `CombatConditionNode` | 条件程序 | 编译后的条件节点、逻辑组合、累计时间与通过次数 |
| `CombatCommandNode` | 命令程序 | 编译后的属性、伤害、Buff、生成子弹/点位、表现命令等节点 |
| `CombatCommandBuffId` | 命令程序 | 命令引用的 Buff ID 列表 |
| `CombatTargetPickerNode` | 选目标程序 | 编译后的最近、距离、方向、随机、子弹、施放者等选目标规则 |
| `CombatSelectedTarget` | 一次触发调用的临时选择 Entity | 最终目标类型与句柄；递归触发有各自的缓冲区 |
| `CombatTargetCandidate` | 临时选择 Entity | 随机选择前的候选索引 |
| `CombatTargetChoice` | 临时选择 Entity | 随机选择后的候选索引 |

`CombatCommandNode` 虽然存于 ECS Buffer，执行器还保存一份不可变 `NativeArray` 快照，并按顺序调用托管 `ICombatCommandCallbacks`；不能据此认为整个命令程序是 Burst Job。`CombatTargetPickerNode` 也会通过托管回调投影目标、读取技能上下文。随机选择继续使用战斗逻辑的随机数回调，维持原有抽取顺序。

通用触发器的目标投影是值类型，目标选择与命令回调使用 `ReferencePool` 复用；递归触发期间各次调用仍取得独立实例。命令回调保留本次命令列表快照，避免递归执行修改正在遍历的列表。自动技能预选与提交的委托按角色缓存，减少每个逻辑步的托管分配。这些改动只降低托管分配，不会把回调提交搬到工作线程。

## Bridge 与辅助类

Bridge 主要把热更对象使用的 `int handle` 映射到 Entity，并提供数据读写及生命周期管理。它们大多是同步静态 API，**不是 ECS System**。

| 类 | 主要职责 |
| --- | --- |
| `CombatBoardBridge` | 棋盘、战斗阶段、固定步累积、全局序号、外观请求队列 |
| `CombatAttributeBridge` | 单位 Entity、属性、位置、阵营、存活、UID 索引及空间版本号 |
| `CombatMovementBridge` / `CombatAttackCastBridge` | 单位移动和施法状态推进 |
| `CombatCooldownBridge` / `CombatBuffTimerBridge` | 冷却与 Buff 创建、查询、更新、释放；手动驱动对应 System |
| `CombatSkillSlotBridge` | 技能槽读写 |
| `CombatProjectileBridge` / `CombatPosPointBridge` | 子弹和点位 Entity 的创建、步进、查询及释放 |
| `CombatConditionProgramBridge` | 条件编译结果、条件判定和点位定时 Job |
| `CombatTargetPickerProgramBridge` | 读取编译后的目标规则并执行选择 |
| `CombatTargetSelectionBridge` | 一次触发的目标 Buffer、快照及 Entity 复用 |
| `CombatCommandProgramBridge` | 命令节点编译存储与顺序解释执行；使用托管回调落地副作用 |
| `CombatTriggerIndex` / `CombatTriggerGroupBridge` | 单位触发器注册索引及分组访问 |
| `CombatBattleResolutionBridge` | 单位死亡后清除棋盘占用并判断胜负 |
| `CombatTargetQueries` / `CombatProjectileBroadphase` | 单位空间查询、子弹阶段的网格索引与邻近候选 |
| `CombatSpatialSelection` / `CombatIdentitySelection` / `CombatProjectileSelection` / `CombatRandomTargetSelection` | 距离、身份、子弹及随机选目标规则 |
| `CombatSpawnPlacement` / `CombatPositionPointPlacement` | 子弹与点位生成坐标计算 |
| `CombatTriggerInvocationBridge` | 串起条件 → 目标选择 → 命令执行，并为递归调用分配独立目标 Buffer |

## 当前边界与性能含义

- **ECS 数据**：单位属性、子弹位置、点位、条件/命令/选目标节点等已经以组件或 Buffer 存储。
- **已并行计算**：冷却、Buff 与攻击施法计时、点位定时推进、曲线子弹路径步进和命中候选、子弹阶段空间索引构建、单位快照上的最近目标和普通技能条件计算。Job 都在使用结果前同步完成。
- **单位快照边界**：先拍摄单位能量、旧目标与位置快照，并行计算最近目标及普通技能的能量/范围结果；`PublishIntentsJob` 随后并行写入各单位 ECS Buffer。该 Job 完成后才推进冷却、Buff 与攻击施法计时，防止首次创建计时 System 时与仍在运行的组件读取 Job 冲突。自动技能就绪意图仍由主线程预选；移动 Job 后的状态 Job 并行筛选普通技能候选，最后按单位顺序提交寻路占位、施法和副作用。当前尚未实现所有战斗效果的帧末统一提交。
- **仍在主线程**：单位的寻路占位与施法提交、复杂触发器递归、命令副作用、逐颗子弹命中处理、GameObject/Transform、伤害数字和音效。命中后可能改变属性、目标或继续生成子弹；简单技能触发器的批量结果按记录顺序在阶段边界提交，其余触发器保留即时路径。

### 简单技能触发事件批次

简单触发器的值类型选择输入、计算结果及范围目标候选现存于棋盘 Entity 的 `CombatSimpleTriggerInput`、`CombatSimpleTriggerResult`、`CombatSimpleTargetCandidate` Buffer。Burst Job 直接读写其 `AsNativeArray` 视图，省去每次 `Flush` 从托管列表复制到临时数组的工作。Job 完成后，按选择序号重新读取 Buffer 值再顺序提交命令；提交期间会创建或回收 Entity，因此不保留可能失效的 Buffer 数组视图。命令事件的托管引用和通用 `OneTrigger` 回退路径仍在主线程，不能将此改动理解为全部触发器已并行化。

`CombatTriggerSimpleQueue` 接受技能本身的 `OnActive` / `SkillBeforeShakeEnd`：无条件、百分比条件或至多 8 层的单子节点条件组，命令列表全部由伤害、固定属性增减、创建子弹、创建点位、单个模板或临时 Buff、移除目标 Buff、单位特效、音效、武器显示及单目标动画组成，选目标是参数单位、施法者、施法者当前目标、单位拥有者、最近目标、持有者距离范围或攻击方向扇形。百分比条件在触发时按原有随机数源抽样一次，单子节点条件组会合并各层取反，Job 使用该值判定；每条命令的数据和身份在触发时快照。扇形边界每次触发只预计算一次，Job 并行判断各单位是否命中。`CombatTriggerSimpleBatch` 从同阶段的单位快照并行选目标与计算单命令伤害；一次触发只选一次目标集合，多个命令共享选择结果，距离范围目标按阵营及 UID、扇形目标按 UID 的原顺序提交副作用。多命令中的伤害在提交时重新读取施法者属性，以保留前面命令对其数值的影响。16 次触发以下直接在当前线程运行，以避免微小批次的 Job 调度成本；范围 Job 只为范围或扇形触发分配单位配对，达到 64 个配对时并行调度。Profiler 的 `Combat.Trigger.Simple.Queued` 计数是进入新路径的触发次数，`Combat.Trigger.Simple.AreaQueued` 是其中的范围和扇形选择次数，`Prepare`、`Commit` 可检查批处理成本。其他条件、不支持的命令或目标类型及 Buff/子弹拥有者仍使用原托管回调。Buff、子弹、点位的实际对象创建仍在主线程；单位特效和动画命令写 ECS 表现请求，播放在表现阶段；技能音效与武器显示命令在 `Commit` 以原顺序调用主线程表现 API；音效播放序号记录在触发器列表中，供停止命令使用。此路径先建立阶段化数据流，`Skill` 和 `OneTrigger` 对象尚未完全退出运行时；后续还需把触发器规则编译成共享 Entity/Blob 并将命令提交改为纯意图消费。

`PerTick` 技能触发也进入简单批次。`OnActive`、`SkillBeforeShakeEnd` 和 `PerTick` 的单子节点定时条件，以及由无条件、百分比、定时条件组成的多子节点条件组，先通过 ECS 条件节点按原短路顺序求值，再把通过的选择与命令写入批次。为保证定时器在目标缺失时仍然推进，这类条件目前只批处理“施法者”和“施法者当前目标”两种选择器；其他选择器保留原路径。条件节点在批次提交完成前保留引用，避免触发期间释放。Profiler 的 `Combat.Trigger.Simple.DataCondition` 可测量这部分条件求值耗时。除下述单节点定时条件外，这里的条件求值与命令提交仍在主线程；目标批量筛选和部分伤害预计算由 `CombatTriggerSimpleBatch` Job 处理，不能将此路径视为通用 `OneTrigger` 已完全多线程化。

直接使用 `ConditionTimed` 或由单子节点条件组包裹一个 `ConditionTimed` 的简单技能触发器，现将每次调用记录为数值事件。同一条件程序的多个事件在 Burst Job 内依触发顺序推进计时状态，不同程序可并行；结果与更新后的计时状态写回 ECS，再由选目标 Job 读取通过标志。少于 8 个独立计时程序时直接运行该 Job，避免调度开销。Profiler 的 `Combat.Trigger.Simple.TimedQueued` 显示进入这条路径的触发次数，`Combat.Trigger.Simple.TimedBatch` 显示批量计时的准备和计算成本。多子节点条件组、物品条件，以及不符合简单命令/选目标规则的触发器仍按原路径在主线程判定。命令结果仍按事件顺序提交，不能把这一段视为完整 `OneTrigger` 并行化。

单个 `ConditionRelateSkill` 的技能类型检查，以及由单子节点条件组包裹它的情况，现在把来源技能类型、参数技能类型和组是否传递参数记录在触发输入中，由 Burst 选目标 Job 判定。含百分比、计时或物品查询的多子节点条件组仍回退到原条件解释器。

只由“无条件”和“技能类型”叶子组成的多子节点条件组，也使用编译后的 ECS `CombatConditionNode` Buffer。`CombatPureSkillGroupLookupSystem` 提供只读 BufferLookup，Burst Job 按组的与/或、节点取反和原有的参数传递规则计算每个触发事件；这些条件不消耗随机数，也不修改计时状态。Profiler 的 `Combat.Trigger.Simple.PureGroupQueued` 和 `Combat.Trigger.Simple.PureGroup` 分别显示事件量与计算成本。含百分比、计时或道具叶子的多子节点组仍由同步解释器执行。

简单批次中的连续固定属性增减命令按目标 UID 记录为 `CombatAttributeIntent`。达到 32 条且至少涉及 4 个单位时，`CombatAttributeBridge.ApplyAttributeIntents` 让各单位在 Burst `IJobChunk` 中顺序更新自己的 `CombatAttribute` 缓冲区；较小批次直接写 ECS 缓冲区，以免调度成本抵消收益。碰到其他命令前会先完成属性批次，保持后续命令读取到的属性顺序。`Combat.Trigger.Simple.Attributes` 标记显示这段成本。

无前后伤害监听、无活动护盾 Buff、护盾值为零且本次不会致死的简单伤害，记录为 HP 变更意图，连续的不同目标可通过同一个 ECS 属性 Job 结算；伤害数字随后写入 ECS 表现事件缓冲区。这条路径跳过原本没有执行任何回调的 `CauseDamageData` 分配。同一目标的重复命中或需要重算伤害的命中在按序提交时会再次尝试 ECS 独立结算；致死伤害、护盾伤害以及有监听器的伤害仍按原路径结算。`Combat.Trigger.Simple.DamageEcs` 标记测量批量路径，`Combat.Trigger.Simple.DamageEcsCount` 显示进入独立结算的命中数。通用触发器回退路径和有副作用的伤害命令仍在主线程。

通用 `OneTrigger` 的命令程序若只包含属性加减节点、空命令和固定次数的重复组，编译时最多展开 512 条属性操作，运行时直接读取命令 Entity 的编译节点及目标选择 Entity 的结果 Buffer，跳过 `CombatCommandCallbacks` 的创建与托管命令列表复制。少量操作按原节点及目标顺序直接更新属性 Buffer；至少 32 次操作且涉及至少 4 个不同单位时，复用并行属性 Job，并保持每个单位收到意图的顺序。`Combat.Trigger.CommandProgram.PureAttributes` 标记可用于确认命中此路径。包含伤害、Buff、生成、表现或依赖道具数量重复的程序仍使用原解释器。

纯属性命令或单条独立伤害命令的单目标选择若是无目标、施法者、施法者当前目标、参数单位、单位持有者或伤害相关目标，可直接把单位句柄写入目标选择 Buffer，不再构造托管目标投影或调用 `CombatTargetPickerCallbacks`。伤害相关目标仍从事件参数读取来源或受击单位，并按原选择器的规则把 `both` 视为受击者；进入直接路径前校验 UID 对应的 Entity 句柄。伤害预检不通过、缺失的当前目标和非单位持有者回退原路径，以保持原有异常及目标类型行为。`Combat.Trigger.Picker.DirectUnit` 可识别此路径。

纯属性命令的最近目标、持有者距离范围和持有者攻击方向扇形选择，现复用 ECS 空间查询并直接写入目标 Buffer。仍用持有者位置、阵营和已编译的选目标节点参数；存在武器长度规则时在主线程解析道具配置。托管单位存在性仍需检查，以保持旧投影筛选结果，但不再为每个结果追加托管目标列表，也不创建 `CombatTargetPickerCallbacks`。`Combat.Trigger.Picker.DirectSpatial` 标记显示该路径。随机、子弹和伤害相关选择仍走原投影。

包含定时条件、技能类型条件和无条件节点的多子节点条件组，现由 `Combat.Trigger.Simple.TimedSkillGroup` 批次交给 Burst。条件运行状态仍保存在程序 Entity 的 `CombatConditionNode` Buffer；同一程序的多次触发按记录顺序逐次判定，不同程序并行。Job 保留 AND/OR 短路、节点取反和“未访问的定时节点不计时”的语义。`Combat.Trigger.Simple.TimedSkillGroupQueued` 统计命中该路径的触发次数。含百分比或物品查询的混合组仍保留原解释器及随机数顺序。

通用命令程序若只包含护盾节点、空命令和固定次数重复组，编译时最多展开 512 条护盾操作；运行时直接读取 ECS 命令数据并按原顺序调用 `CombatBuffTimerBridge.ApplyShield`，不再创建 `CombatCommandCallbacks` 或复制 `CommandBase` 列表。护盾依赖同一 Buff 和单位属性的前序结果，提交仍在主线程保持顺序。Profiler 标记为 `Combat.Trigger.CommandProgram.PureShields`。

Buff 剩余时间由 `CombatBuffTimerSystem` 并行推进。单位更新时 `CollectDueForOwner` 只把已到期或本帧重设计时的活跃 Buff 交给主线程，并按原 `ActiveOrder` 执行到期销毁；其他活跃 Buff 无需逐个调用托管 `UpdateTimer`。Buff 销毁会触发技能/光环副作用，因此销毁回调仍保留在顺序提交阶段。

单位的 `PerTick` 仍从 `EntityQizi.UpdateSkill` 调用 `OnTrigger`。单位 Entity 的 `CombatTriggerListener` 当前只保存触发类型和注册 ID；对应规则、条件状态、目标选择及命令副作用尚未全部由该 Buffer 直接驱动。要移走这个主线程入口，需要先让注册项指向完整的规则定义与运行状态，再由 System 产生有序触发事件并消费命令意图。Buff 入栈检查和受伤处理复用同一个缓存的存在性委托，避免每次创建 Buff 都分配闭包。

通用命令程序若恰好是一条伤害命令，会先对全部不同单位目标预检：施法者存在、目标存活且不是施法者、无相关伤害监听、无护盾和护盾 Buff、本次不致死。全部满足时直接更新 ECS HP 并写入伤害数字事件，跳过 `CauseDamageData` 和 `CombatCommandCallbacks`；任一目标不满足则整次调用退回原伤害回调，避免部分目标提前结算。`Combat.Trigger.CommandProgram.IndependentDamage` 标记显示该路径的实际提交时间。这条路径仍在主线程按目标顺序提交；致死、护盾及有监听的伤害保持原有副作用顺序。

上述独立伤害一次命中至少 32 个不同单位时，会先校验每个目标的 UID 与 Entity 句柄，再把 HP 变更写入 `CombatAttributeIntent` 列表，由 `ApplyAttributeIntents` 的 Burst Job 并行处理不同单位。Job 完成后，仍按原目标顺序写入伤害数字事件。`Combat.Trigger.CommandProgram.IndependentDamage.Parallel` 标记显示这段并行结算的调度与等待时间；较少目标继续直接逐个提交。

简单触发器中的特效、动画、音效和武器命令在录制时只保存命令参数，不保存相应 `CommandBase` 运行时对象。提交时特效进入单位 Entity 的表现事件 Buffer，动画进入单位 Entity 的动画请求；对应的 Unity 对象更新仍在表现阶段主线程完成。音效播放与武器对象仍在主线程按命令顺序执行：音效的播放序号存于当前技能状态，武器归属仍以 `Skill` 实例标识。这两类状态还需要拆成 Entity 数据及独立表现提交，才能彻底去除技能实例依赖。

简单触发器的表格 Buff 创建命令只记录 Buff ID，提交时按 ID 读取模板，不再保留 `CommandCreateBuff` 对象。临时 Buff 仍需引用可克隆的 Buff 定义，因此保留命令对象直到规则定义和运行状态分离。两类 Buff 的实际创建、`OnActive` 以及后续回调目前仍是托管提交，不属于并行 ECS 结算。

穿透子弹的简单命中及批量创建队列复用同一套无副作用伤害判定；符合条件时直接修改 ECS HP 缓冲区并写伤害数字事件，随后按原顺序处理命中特效和音效。`Combat.Bullet.DamageEcsCount` 显示命中队列中走此路径的次数。这种单次命中的直接结算仍由主线程提交，只有上面满足批量阈值的独立伤害使用 Job。

简单技能触发器现在也接受上述命令嵌套在 `RepeatExecute` 中的情况。入队时计算重复次数并展开为原执行顺序的命令事件，所有事件复用一次目标选择；嵌套最多 8 层，每次触发最多展开 512 条命令，超限则走原同步路径。`Combat.Trigger.Simple.CommandEvents` 记录展开后的事件数，可与 `Queued` 对照判断重复命令造成的提交量。展开并未把对象创建、伤害回调或 Buff 副作用搬到工作线程；这些仍在 `Commit` 主线程执行。
- **表现拆分**：子弹外观请求进 `CombatProjectileVisualRequest`，逻辑伤害不等外观；不支持 ECS 渲染的资源仍在 Unity 主线程从 GameObject 池获取，并以 `Combat.Presentation.BulletVisuals` 标记测量。
- **普通 Mesh 子弹外观**：子弹表引用的单 Mesh 资源在布阵时构建 Entities Graphics 渲染原型，战斗中用 Burst Job 与并行 `EntityCommandBuffer` 批量实例化外观 Entity，表现 System 从逻辑子弹 Entity 读取位置及方向并在逻辑子弹销毁后删除外观。资源不符合单 Mesh 条件或原型尚未准备好时继续使用 GameObject 池。飞剑拖尾、粒子特效和其他表现仍采用原路径；ECB 回放和首次渲染原型创建仍有主线程工作。
- **点位锚点**：`EntityPosPoint` 只保留 ECS 位置与数值朝向，不再为每个点位从空 GameObject 池取锚点。特效回调按位置与朝向设置已有的特效对象；点位不在生成后移动。
- **逻辑子弹 Entity 池**：跟踪、标记点、环绕、曲线子弹使用各自的实体池。回收时清空组件与曲线命中 Buffer，关闭 `CombatProjectileActive`；查询自动排除闲置实体。布阵阶段分帧预热，发射时复用已有架构并重新启用。外观 Entity 还校验生成序号，避免逻辑 Entity 复用后跟随另一颗子弹。
- **逻辑点位 Entity 池**：点位 Entity 同样在布阵阶段预热，释放时清空状态并关闭 `CombatPosPointActive`，下一次生成复用；点位查询排除闲置实体。
- **点位初始化**：生成坐标、朝向、阵营与持续时间先计算，再一次写入 `CombatPosPoint` 组件；点位触发器配置在该写入之后执行，避免每个字段分别通过句柄读取和写回 Entity。
- **池命中观测**：`Combat.Pool.Projectile.Misses` 与 `Combat.Pool.Point.Misses` 是累计池未命中次数，`Combat.Pool.Projectile.Active` 与 `Combat.Pool.Point.Active` 显示活跃数量。战斗中 Misses 增长说明预热数量不足，仍发生主线程实体创建。
- **战前预热**：布阵阶段预热当前子弹表引用的预制体与空点位对象。空对象复用时保留在固定根节点并用激活状态控制，减少点位生成/释放时的父节点切换；这些 Unity 对象操作依然在主线程。
- **触发器树复用**：回收子弹和点位时释放条件、命令、选目标 ECS 程序句柄并清除 Owner/Skill，但将托管触发器树留在对应的池对象上。下次生成按模板逐节点覆盖，同类型节点复用，类型或数量改变时替换或释放多余节点；空触发器模板直接跳过克隆。这样减少同一批角色发射时的托管对象申请，触发器执行及 GameObject 创建仍在主线程。
- **候选回退**：命中候选表只用于轨迹、阵营和单位空间版本都未变化的曲线子弹；任何不匹配会退回原空间索引或实时扫描。已命中 Buffer 与触发器依旧按子弹生成顺序和单位 UID 更新。
- **GC 与 ECS**：临时快照、List/Dictionary、托管技能对象、Unity 对象池仍可能产生分配。Profiler 的 `GC Alloc` 表示分配量，只有出现 GC 回收标记时才能确认该帧发生回收。

排查性能时，先比较同一战斗阶段、同一逻辑步数下的 `Combat.Unit.PlanSnapshot`、`Combat.Unit.PlanComplete`、`Combat.Step.UnitIntents`、`Combat.Step.Units`、`Combat.Step.HitCandidateSchedule`、`Combat.Step.HitCandidateComplete`、`Combat.Bullet.Penetrating.BatchCandidates`、`Combat.Step.Projectiles`、`Combat.Step.Points`、`Combat.Trigger.CommandProgram` 和 `Combat.Presentation.BulletVisuals`。如果命令回调或对象创建占主峰，把对应代码移进 ECS Buffer 本身不会降低主线程时间；需要先划清可并行的纯计算和必须按顺序应用的副作用。

## 触发器完整迁移的执行边界

当前 `OneTrigger` 是托管对象，`CombatConditionNode`、`CombatTargetPickerNode`、`CombatCommandNode` 是编译后的 ECS Buffer。条件判定仍调用托管的随机数、物品查询；命令解释器仍按节点顺序调用 `CombatCommandCallbacks`。因此 `Combat.Trigger.CommandProgram` 是同步执行时间，不能仅通过把 `OneTrigger` 改名或附到 Entity 上变为并行 Job。

普攻前摇已有一条直接执行路径：`CombatAttackCastSystem` 从技能 Entity 的有序链接写出触发器 Entity；当规则为无条件，目标选择为施法者、拥有者、当前目标、最近目标或固定半径范围，且命令全是属性修改时，提交阶段直接读取触发器的 ECS Buffer，写入原有帧末属性批处理。此组合不调用 `OneTrigger.OnTrigger`，其余规则仍走旧回调。

伤害调用现在把来源技能、施法者、受击者、数值和前后阶段写入棋盘的 `CombatDamageEvent` Buffer；嵌套伤害使用栈顺序入队和出队。单位的 `CombatTriggerListener` 可直接指向已编译的触发器 Entity。无条件、单目标、仅修改属性的伤害前后监听会直接读该 Entity 的规则并同步执行，以保持“前置触发 → 护盾/血量 → 后置触发 → 死亡”的依赖顺序。未匹配的规则与复杂命令仍走原 `OneTrigger` 回调；当前伤害结算本身也尚未成为并行 Job。

激活的单位 Buff 现在把全部触发规则编译到其计时 Entity 所持有的 `CombatBuffTriggerLink`，释放计时 Entity 时销毁规则 Entity。受伤监听因此也可引用 Buff 触发器 Entity。单目标、只含属性与当前 Buff 护盾命令的激活、每步、销毁及受伤规则（含固定次数重复），在无条件、检查来源技能类型，或由这两类节点和定时节点组成条件组时，按命令顺序从 Entity Buffer 同步执行，并区分来源技能施法者与 Buff 持有者。定时节点要求目标为 Buff 持有者，累计时间和触发次数保存在规则 Entity 的条件 Buffer；条件组保留原短路顺序。Buff 的其他条件、Buff 创建/移除和复杂伤害副作用仍调用托管 `Buff`、`OneTrigger`；标记弹持有的 Buff 尚未接入这条单位 Buff 编译路径。

1. **定义**：技能、Buff、子弹、点位的触发器模板编译为不可变的 ECS 定义实体。实例 Entity 只保存定义引用、拥有者句柄、定时/次数等可变状态；生成子弹时不克隆整棵托管触发器树。`CommandBase` 留作编辑器与导入数据的配置类型，不参与热路径执行。
2. **事件**：命中扫描和单位状态更新只写 `CombatTriggerEvent`，包含逻辑帧、阶段、来源生成序号、触发器序号、目标 UID 与原始事件参数。该阶段不执行命令。数据只使用可在 Job 中读取的 Entity/句柄及数值，不能包含 `EntityBase` 或委托。
3. **同帧快照计算**：在事件批次开始前固定单位属性、位置、阵营、技能及相关物品数据。Burst Job 并行求条件、选目标、伤害公式及坐标，输出 `CombatCommandIntent`。随机数按逻辑帧、来源序号、触发器序号和触发次数派生，结果不依赖 Job 调度顺序。定时条件计数写回各实例自己的 ECS 状态。
4. **确定顺序提交**：按阶段 → 来源生成序号 → 触发器序号 → 命令序号 → 目标 UID → 重复次数稳定排序意图。伤害、Buff、移除与实体生成按此顺序提交；生成和伤害引出的新事件进入下一轮批次，同帧循环直至队列为空，并设置明确的递归上限。提交前复核实体存活和生成序号，避免复用句柄误指向新对象。GameObject 粒子、音频和动画仅消费提交后的表现请求。
5. **逐类替换**：先迁移截图中最热的穿透子弹 `OnBulletHitTarget` 的条件、选目标及 `CreateProjectile` 命令；对比相同逻辑帧下命中目标、伤害、子弹生成序号与随机结果。然后覆盖点位、普通技能、Buff 和其余命令，最后删除热路径的 `CombatCommandCallbacks` 与托管触发器克隆。每一类未迁完前保留原同步路径，不能把新旧路径同时执行。

这个迁移会把“事件计算”和“副作用提交”分开。并行计算能缩短条件、选目标、公式和生成参数准备时间；EntityCommandBuffer 回放、必须按顺序生效的战斗变化及 Unity 表现仍需同步成本。旧逻辑允许命中后立刻改变同一次扫描后续目标；迁移采用同帧快照与批次提交，必须在对照战斗中验证这项规则变化。

### 第一条已接入的命中批处理路径

穿透子弹的 `OnBulletHitTarget` 现在会检查整组触发器：仅当每个匹配触发器都是**无条件、参数目标、命令只包含伤害和创建子弹且至少创建一次子弹**时，才记录命中时的拥有者位置、目标位置、攻击属性和命令数值。`CombatTriggerProjectileSpawnBatch` 用 Burst `IJobParallelFor` 并行计算伤害公式与生成坐标；`CombatTriggerProjectileSpawnQueue` 在子弹阶段末按原命中顺序提交伤害和生成命令。子弹提前回收时，先提交尚未处理的事件，避免池复用使触发器引用失效。任一条件不满足时整组退回原同步路径，不会混合执行。

Profiler 计数 `Combat.Trigger.ProjectileSpawn.Queued` 与 `Combat.Trigger.ProjectileSpawn.Fallback` 分别显示本逻辑步进入新路径的创建命令数和不符合条件的穿透命中次数；`Prepare` 与 `Commit` 标记区分 Job 计算和主线程创建成本。若 `Queued` 一直为零，这项改动不会改善当前战斗场景。现阶段 `OneTrigger`、嵌套触发器克隆、`CreateBullet` 的对象创建仍为托管主线程操作；触发器完整 Entity 定义及伤害/Buff/点位命令迁移仍待完成。

`Combat.Trigger.ProjectileSpawn.Commit` 内的简单命中现在细分为 `SimpleHit.Damage`、`SimpleHit.Sfx`、`SimpleHit.Audio`；新子弹创建已有 `Combat.Bullet.CreateCommand` 标记。需在同一峰值帧展开这些子项，才能区分伤害副作用、表现 API 与对象创建的成本。

少于 64 条意图时，`Prepare` 用复用的结果数组在主线程直接计算，以免临时 NativeArray 分配和 Job 调度开销超过公式与坐标计算；达到 64 条才并行。当前截图约 24 次创建命令，峰值更可能继续落在 `Commit` 的对象/触发器创建，而非此 Job。请以同一战斗阶段的 `Queued`、`Fallback`、`Prepare`、`Commit` 结果决定下一步迁移对象。

### 子弹命中四命令路径

技能模板 2003/2004 的子弹激活规则为无条件、选择子弹自身、播放一条音效；命中规则为无条件、选择命中参数目标，命令顺序是伤害 → 特效 → 停止音效 → 播放音效。`CombatBulletSimpleHitCompiler` 只在完整匹配这两条规则且没有其他子弹触发器时，把数值写入子弹 Entity 的 `CombatProjectileSimpleHit`；这类子弹不再克隆托管 `OneTrigger`。不匹配的模板保留原路径。

`CombatProjectileActiveAudioSystem` 并行标记新子弹的激活音效，`CombatFrameScheduler` 在单位阶段后及点位阶段后消费，主线程调用音效 API，并将返回的播放序号写回子弹组件。命中提交从组件读取序号，按旧命令顺序停止旧音效、播放新音效。音效对象与播放 API 仍在主线程。Profiler 的 `Combat.Presentation.BulletAudio` 可单独检查这部分成本。

命中时 `CombatTriggerProjectileSpawnQueue` 读取 Entity 规则并记录一条有序事件及施法者属性快照，与原有穿透命中事件一起批量计算伤害，再按记录顺序提交伤害、特效与音效。简单命中事件以子弹句柄、生成序号、目标/施法者 UID 和命中位置保存，提交时才解析表现对象；复杂伤害回退仍保留来源 Skill 引用。子弹回收前先提交待处理事件，避免池复用。Profiler 的 `Combat.Bullet.EcsSimpleHits` 显示本步进入此路径的命中数。若为零，说明当前场景未使用该组合。按帧末结算后，同一步后续命中会先按快照扫描，再在提交时复核目标存活；与旧的即时结算顺序可能不同，需对照战斗验证。

简单命中队列现在也提供按子弹 Entity 句柄、生成序号和施法者 UID 入队的接口；旧 `BulletBase` 只负责转发这些字段。回收前可按句柄冲刷未提交命中。这样后续子弹更新系统可以不经 `BulletBase` 记录命中，但目前生成及四类逐弹回调仍保留托管包装；复杂伤害仍可能需要来源 `Skill`。

简单追踪弹（没有托管子弹触发器、命中规则已编译为 `CombatProjectileSimpleHit`）现在直接消费并行位移结果并按 Entity 句柄入队命中事件，不再通过 `BulletTracking.LogicUpdate` 和 `OnHitTarget`。目标丢失或命中后仍借用 `BulletBase.OnDead` 回收现有包装和表现对象；复杂规则仍走原回调。此改动减少逐弹触发器桥接，尚未取消主线程逐弹分发、包装创建或 Unity 表现。

### 点位 OnActive 的 ECS 运行时路径

点位的 `OnActive` 若全部是无条件、选择触发器拥有者、仅创建或移除特效及播放或停止音效，则从技能配置读出表现规则，按原命令顺序写入点位 Entity 的 `CombatPointPresentationRule` Buffer，并把激活写入 `CombatPointTriggerEvent`。`CombatPointTriggerSystem` 在逻辑步末并行处理事件，按规则顺序写 `CombatPointPresentationIntent`。渲染更新在主线程按点位生成顺序消费意图，执行特效和音效 API；音效播放序号保存在点位 Entity 上，供后续停止命令读取。这条路径不再为 `OnActive` 克隆 `OneTrigger`，只有点位的其他触发类型还会克隆；没有其他触发类型时点位不持有托管 `TriggerList`。不符合条件的 `OnActive` 整组回退原路径。

系统仅在有激活事件时调度，避免每逻辑步无条件安排 Job。Profiler 的 `Combat.Point.EcsTriggerEvents` 显示本逻辑步进入新路径的事件数；`Combat.Presentation.PointEffects` 单独标记主线程特效和音效播放成本。事件计算的并行化不会消除 SFX 的 GameObject 创建、资源加载和 Transform 操作；它们仍是主线程表现工作。

点位 `PerTick` 若只有一条有命令的定时条件，支持两类规则：参数目标上的创建子弹；触发器拥有者上的特效与音效命令。计时与通过次数保存在点位 Entity，由 `CombatPointTimedTriggerSystem` 并行推进。子弹命令按原点位顺序提交；特效和音效由主线程表现阶段消费。仅确定无副作用的空命令距离探测会被跳过。模板 2003/2004 的主要点位使用子弹路径，2004 的另一种点位使用特效/音效路径。符合条件的点位不再克隆 `PerTick` 的 `OneTrigger`。其他条件、选目标或命令组合仍使用原触发器路径。Profiler 的 `Combat.Point.EcsTimedBulletPoints` 和 `Combat.Point.EcsTimedPresentationPoints` 显示活跃的新路径点位数量，`Combat.Point.TimedProjectileCommit` 显示主线程创建子弹的成本。

点位的子弹命令配置仍会按点位复制；符合上述两规则的子弹生成时不再克隆嵌套触发器。技能、Buff 和其他子弹模板的普通 `OnTrigger` / `OnActive` 尚未改为纯 ECS 运行时。完整迁移还需要把这些实例的可变条件状态、事件参数、目标选择及命令意图放在 Entity 上，并按确定顺序提交副作用。



