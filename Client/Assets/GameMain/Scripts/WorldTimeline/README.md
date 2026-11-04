# 世界时间线核心

纯 C# 核心，无 Unity 引用，暂未接入正式游戏流程。所有角色在元年（内部 Year=0）出生。可操作的独立 Unity 调试场景已实现，启动与操作见 [WorldTimelineDebug/README](../WorldTimelineDebug/README.md)。

## 使用

1. 创建 WorldState，加入节点和角色。PlayerActorId 必须存在，角色 ID 在本局唯一。
2. 创建 WorldSimulation。先用 AssignAction 为其他角色分配行动（不会推进时间）。
3. 调用 SubmitPlayerAction 提交玩家行动，世界推进到该行动结束或玩家死亡。
4. Events 提供有序的结果日志；WaitingForPlayer 表示可以显示下一轮选项。
5. AdvanceOneYear 是观察模式/调试用的底层入口，可以在玩家死亡后继续模拟。

每年顺序：历史角色决策、改命角色寻路、天选决策 → 增加年龄及寿元死亡 → 更新行动剩余年数并登记全部抵达 → 改命角色与天选会合加入 → 普通角色按 ID 连续战斗 → 天选挑战提前伪装的胜者 → 节点怪物和物品结算 → 存活者获得当年修炼经验 → 完成行动并检查改命。

移动开始即离开原节点，抵达前 NodeId=null。节点驻留名单由角色位置推导，避免维护两份数据。只有有新抵达者的节点触发遭遇，驻留者同样参战。死亡清空行动和位置。连续战斗保留剩余血量。

## 当前范围

- 支持移动、修炼、等待，正整数年耗时，节点连通性校验。
- 支持寿元死亡、普通角色遭遇、节点怪物、唯一宝物消耗。
- 默认战斗为先手回合攻击的简化模型，通过 IWorldBattleResolver 替换；不代表最终战斗设计。
- 支持玩家人生快照与历史行动自动续接：出生属性、初始物品、行动开始年份和行动请求、相关结果日志、实际死亡年份。
- 历史角色使用本局节点与战斗状态，不复制旧奖励、战斗胜负或强制死亡。原始记录只读；新角色使用本局唯一实例 ID。
- 历史角色存活至原定死亡年份后标记 FateChanged，取消尚未结束的旧行动。已配置天选时，开始寻找路线、追赶和加入；天选死亡后改为前往最终节点。未配置天选则停留原地。非死亡快照正常播完后标记 RecordFinished；缺失连接时标记 RouteBlocked，不瞬移。
- 天选按固定连通路线移动，默认每段十年；通过击败怪物或伪装角色吸收生命和攻击的百分比，默认 10%，向下取整。当前生命增加与生命上限相同的吸收量。
- 主动与天选同节点不自动战斗，可以加入或移动离开。提前伪装才成为敌人；天选已经在场时不能伪装。等待保留伪装，移动或修炼解除伪装。
- 加入是零耗时交互，记录并支持回放；队友不再执行行动或获得修炼收益，但会承受战斗伤害和寿元消耗。玩家加入后永久进入观察，即使天选死亡也不恢复控制。
- 玩家在伪装战斗中杀死天选立即结束本局，之后不再推进年份。其他原因导致天选死亡不会停止玩家行动。
- 支持最终 Boss 仪式：普通角色击败后重置倒计时并反噬死亡，Boss 恢复完整属性；天选击败后通关；截止年开始时仪式灭世。
- 已支持配置驱动的经验升级和各属性独立资质，详见调试场景说明。尚未实现战败物品掉落、保存读档及正式 UI。没有新行动的普通角色停留原地，不会自动成长。
- 本阶段世界状态由主线程单一调用方维护，初始化后不要直接修改集合。事件日志暂存内存，未来接入记录器后按需消费。
- Year=0 目前显示语义为元年；未来 UI 必须统一年份显示规则。

## 验证

在仓库根目录运行 `dotnet run --project Tests/WorldTimeline/WorldTimeline.Tests.csproj`。
测试直接编译 Unity 使用的同一份源码，无第三方测试依赖。

## 历史回放接口

```csharp
// 原局执行过行动后，导出独立快照；玩家死亡后观察不会延长死亡年份。
LifeRecord record = originalSimulation.RecordPlayerLife();

// 下一局先初始化地图和当前玩家，再于元年载入选中的记录。
HistoryActorController history = nextSimulation.AddHistory(record, instanceId: 10);
nextSimulation.SubmitPlayerAction(WorldAction.Cultivate(5));
// 历史角色在每年开始时检查行动，按原开始年份自动执行。
// history.Status 可区分回放、记录结束、改命、死亡和路线阻断。
```

RecordedAction 保存请求耗时，即使原角色途中死亡也保留原请求；死亡由新局实际情况重新决定。快照可以在角色存活时导出，此时只代表已记录部分，并非完整一生。当前仅内存记录，不代表已实现磁盘存档。年内顺序不因是否加载历史角色改变。

## 天选接口与原型规则

```csharp
// 元年配置，角色 ID 不能与当前玩家或历史角色重复。
simulation.ConfigureChosen(new ActorState(20, birthNodeId, health: 200, attack: 100, lifespan: 500),
    new[] { birthNodeId, firstNodeId, finalNodeId }, yearsPerStep: 10, absorptionPercent: 10);

// 同节点才允许加入；也可正常移动离开。加入后由界面按需调用 AdvanceOneYear 进行观察。
if (simulation.CanJoinChosen(playerId))
    simulation.SubmitPlayerAction(WorldAction.JoinChosen());

// 提前抵达目标节点后伪装，然后等待天选。伪装不与移动合并。
simulation.SubmitPlayerAction(WorldAction.Disguise());
simulation.SubmitPlayerAction(WorldAction.Wait(5));
```

寻路暂按每边一年计算，最短路径并列时按节点 ID 决定。天选途中 NodeId=null，追赶者朝其当前目的节点追赶；会合按实际年份和位置判断，不瞬移。天选在元年出生点暂不主动结算关卡，路线从下一个节点开始挑战。

队伍战斗暂以存活成员生命与攻击之和结算，队友按 ID 承伤，天选最后承伤。此为可替换的数值原型，尚未接入现有自走棋战斗。普通角色先互相争斗，胜者先于天选处理节点怪物；提前清怪会减少天选吸收来源。只有天选在场时，才挑战伪装敌人。

天选途中寿元耗尽时，存活队友回到该段出发节点并脱离队伍，避免直接跳过未完成路段。已改命的历史队友下一年继续向最终节点移动；玩家保持观察。未配置最终仪式时，最终节点仍然只是普通路线终点，不自动通关。

## 最终仪式与结局

```csharp
// 配置天选之后、元年内调用，Boss 节点与天选路线终点一致。
simulation.ConfigureFinalRitual(bossHealth: 1000, bossAttack: 100, periodYears: 100);
int deadline = simulation.State.Ritual.DeadlineYear;

// 玩家死亡或加入后，由表现层有节制地调用；每次仍然只算一年。
if (!simulation.State.IsEnded)
    simulation.AdvanceOneYear();

RunOutcome outcome = simulation.State.Outcome;
```

- 首次截止年为周期年数，例如元年开始积蓄，第一百年开始时灭世。截止年之前必须赢下最终战，第一百年才抵达已经来不及。
- 普通角色获胜：截止年改为「当前年 + 周期」，ResetCount 加一，Boss 立即恢复配置生命和攻击；只杀获胜的挑战者。不会削弱 Boss，不杀地图上其他角色，不重复发放最终关卡宝物。
- 战败不重置仪式。Boss 战败前受到的伤害保留，直到一次成功重置时恢复。天选死亡后仪式仍会继续，玩家与历史角色照常行动。
- 天选获胜：Outcome=Cleared，保留存活角色，停止全部行动。不再触发重置或仪式反噬。
- 到期灭世：Outcome=WorldDestroyed，所有存活角色死亡，停止所有后续行动和当年奖励。
- 玩家杀死天选：Outcome=PlayerKilledChosen，立即终止。三类结局都会使 IsEnded=true，后续 AdvanceOneYear 不再增加年份，提交行动被拒绝。
- 玩家死亡本身不设置终局，仍能观察天选；人生记录保留真实死亡年份。未配置仪式时，观察调用方应自行限制推进年数。
- 同年多个普通角色抵达最终节点仍先按 ID 战斗，胜者挑战 Boss，因此不会每个抵达者都获得重置。同年天选也抵达时，普通胜者先挑战并反噬，天选随后面对恢复后的 Boss。

长时间验证场景：初始截止第一百年，历史角色分别在第九十、一百八十、二百七十、三百六十年挑战成功，截止依次延后到第一百九十、二百八十、三百七十、四百六十年，天选第四百年抵达并通关。该测试中为接力角色和天选显式配置了足够寿元，玩家第一百年自然死亡后继续观察。

升级配置入口为 CultivationConfig / CultivationSystem，示例表位于 WorldTimelineDebug/Resources/WorldTimeline/CultivationLevels.csv。第一级是出生值，后续级别为加法增量，历史记录保留出生资质。ActorMapProjection 给出所有存活角色（含移动和跟随状态）的地图位置。
