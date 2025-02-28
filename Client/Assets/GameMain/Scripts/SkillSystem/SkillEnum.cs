using UnityEngine;

namespace SkillSystem
{
    /// <summary>
    /// 触发类型,（新类型只能往后加
    /// </summary>
    public enum TriggerType
    {
        /// <summary>
        /// 触发时
        /// </summary>
        [InspectorName("触发时")]
        OnActive,
        [InspectorName("结束时")]
        OnDestory,
        /// <summary>
        /// 每帧触发
        /// </summary>
        PerTick,
        /// <summary>
        /// 释放普通攻击时
        /// </summary>
        OnNormalAttack,
        /// <summary>
        /// 被释放普工时
        /// </summary>
        OnBeNormalAttack,
        /// <summary>
        /// 技能前摇结束
        /// </summary>
        [InspectorName("技能前摇结束")]
        SkillBeforeShakeEnd,
        [InspectorName("伤害计算完将要造成伤害前")]
        BeforeCauseDamage,
        [InspectorName("伤害计算完将要 被 造成伤害前")]
        BeforeBeCauseDamage,
        [InspectorName("造成伤害后")]
        AfterCauseDamage,
        [InspectorName("被 造成伤害后")]
        AfterBeCauseDamage,
        [InspectorName("子弹命中目标")]
        OnBulletHitTarget,
        [InspectorName("技能释放前")]
        BeforeSkillCast,
        [InspectorName("技能释放后")]
        AfterSkillCast,
    }

    public enum ConditionType
    {
        /// <summary>
        /// 无条件通过
        /// </summary>
        [InspectorName("无条件通过")]
        NoCondition,
        /// <summary>
        /// 条件组
        /// </summary>
        [InspectorName("条件组")]
        ConditionGroup,
        /// <summary>
        /// 概率
        /// </summary>
        [InspectorName("概率触发")]
        Percentage,
        [InspectorName("定时触发")]
        Timed,
        [InspectorName("道具相关")]
        RelateItem
    }
    public enum LogicOperator
    {
        [InspectorName("与")]
        And,
        [InspectorName("或")]
        Or
    }
    public enum TargetPickerType
    {
        [InspectorName("无目标")]
        NoTarget,
        /// <summary>
        /// 当前普工目标
        /// </summary>
        [InspectorName("技能释放者当前目标")]
        SkillCasterCurTarget,
        [InspectorName("技能释放者")]
        SkillCaster,
        /// <summary>
        /// 最近的目标（能选中的）
        /// </summary>
        Nearest,
        [InspectorName("触发参数传递进来的目标")]
        Arg,
        [InspectorName("触发器持有者")]
        TriggerOwner,
        [InspectorName("伤害相关目标")]
        RelatedDamageTarget,
        [InspectorName("持有者当前方向扇形")]
        OwnerDirection,
        [InspectorName("随机选取")]
        RandomFromList,
        [InspectorName("选取子弹")]
        Bullet,
    }

    public enum DamageDataTargetType
    {
        [InspectorName("伤害来源")]
        Caster,
        [InspectorName("伤害受击者")]
        Target,
        [InspectorName("伤害来源和受击者")]
        both,
    }

    public enum CommandType
    {
        [InspectorName("啥事不干")]
        DoNothing,
        /// <summary>
        /// 造成伤害
        /// </summary>
        [InspectorName("造成伤害")]
        CauseDamage,
        [InspectorName("添加buff")]
        CreateBuff,
        [InspectorName("播放动画")]
        PlayAnim,
        [InspectorName("创建子弹")]
        CreateBullet,
        [InspectorName("创建护盾")]
        CreateHuDun,
        [InspectorName("创建特效")]
        CreateSfx,
        [InspectorName("显示武器")]
        ShowWeapon,
        [InspectorName("重复执行")]
        RepeatExecute,
        [InspectorName("销毁buff")]
        RemoveBuff,
        [InspectorName("销毁bullet")]
        RemoveBullet,
    }

    /// <summary>
    /// 伤害计算类型
    /// </summary>
    public enum DamageComputeType
    {
        [InspectorName("普攻")]
        NormalDamage,
        [InspectorName("固定值加属性百分比")]
        FixNumAddAttrPercent,
    }
    public enum GenerateEnumDataTables
    {
        [InspectorName("不读表固定值")]
        None,
        [InspectorName("SKill表")]
        Skill,
        [InspectorName("Buff表")]
        Buff,
    }

    public enum SkillType
    {
        /// <summary>
        /// 普工
        /// </summary>
        NormalSkill = 1,
        /// <summary>
        /// 无动画攻击技能
        /// </summary>
        NoAnimSkill = 2,
        /// <summary>
        /// 特殊技能
        /// </summary>
        SpSkill = 3,
        /// <summary>
        /// 被动技能
        /// </summary>
        PassiveSkill = 4,
    }
    /// <summary>
    /// buff标签
    /// </summary>
    public enum BuffTag
    {
        [InspectorName("无")]
        None = 0,         // 00000000
        [InspectorName("重伤")]
        HeavyDamage = 1 << 0,  // 00000001
        [InspectorName("冰冻")]
        Frozen = 1 << 1,       // 00000010
        [InspectorName("眩晕")]
        Stunned = 1 << 2,      // 00000100
        [InspectorName("流血")]
        Bleeding = 1 << 3,     // 00001000
        [InspectorName("燃烧")]
        Burning = 1 << 4,      // 00010000
        [InspectorName("治疗")]
        Healing = 1 << 5,      // 00100000
        [InspectorName("不可免疫")]
        Immune = 1 << 6,        // 01000000
        [InspectorName("护盾")]
        Shield = 1 << 7        // 01000000
    }

    public enum CheckCastSkillResult
    {
        /// <summary>
        /// 技能错误
        /// </summary>
        Error,
        /// <summary>
        /// 能释放
        /// </summary>
        CanCast,
        /// <summary>
        /// 没有能量
        /// </summary>
        NoPower,
        /// <summary>
        /// 目标在范围外
        /// </summary>
        TargetOutRange,
        /// <summary>
        /// 普攻还在等待
        /// </summary>
        NormalAtkWait,
        /// <summary>
        /// 没有有效目标
        /// </summary>
        NoValidTarget,
    }

    public enum SkillCastTargetType
    {
        NoNeedTarget = 0,
        /// <summary>
        /// 最近的敌人
        /// </summary>
        NearestEnemy = 1,
    }

    public enum CampType
    {
        Enemy,
        Friend,
        Both,
    }

    public enum AttributeType
    {
        [InspectorName("当前血量")]
        Hp,
        [InspectorName("最大血量")]
        MaxHp,
        [InspectorName("当前蓝量")]
        Power,
        [InspectorName("最大蓝量")]
        MaxPower,
        [InspectorName("护盾")]
        HuDun,
        [InspectorName("护盾增益")]
        HuDunBoost,
        [InspectorName("技能急速")]
        CooldownReduce,
        [InspectorName("攻击力")]
        AttackDamage,
        [InspectorName("普攻速")]
        AttackSpeed,
    }
    public enum BulletType
    {
        [InspectorName("追踪子弹")]
        TrackingBullet,
        [InspectorName("围绕持有者旋转子弹")]
        RotateOwner,
        [InspectorName("穿透子弹")]
        PenetratingBullet,
        [InspectorName("标记物")]
        MarkPoint,
    }

    public enum WeaponHandleType
    {
        [InspectorName("无父物体")]
        None,
        [InspectorName("左手")]
        LeftHand,
        [InspectorName("右手")]
        RightHand,
    }

    public enum ConditionRelateItemFrom
    {
        [InspectorName("来源技能道具")]
        SkillItem,
        [InspectorName("来自参数中的技能道具")]
        ArgSkillItem,
    }

    public enum ConditionRelateItemCheckType
    {
        [InspectorName("由目标道具合成")]
        ContainItem,
    }

    public enum NumberCheckType
    {
        [InspectorName("固定次数")]
        FixedNumber,
        [InspectorName("来源技能含子道具的数量")]
        ParentSkillContainSubItemNumber,
        [InspectorName("arg技能含子道具的数量")]
        ArgSkillContainSubItemNumber,
    }

    public enum RelateEntityType
    {
        [InspectorName("持有者")]
        Owner,
        [InspectorName("释放者")]
        Caster,
        [InspectorName("目标")]
        Target,
        [InspectorName("参数中角色")]
        Arg,
    }
}