using UnityEngine;

namespace SkillSystem
{
    /// <summary>
    /// 触发类型
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
        Timed
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
    /// <summary>
    /// 伤害类型
    /// </summary>
    public enum DamageType
    {
        [InspectorName("物理伤害")]
        PhysicalDamage,
        [InspectorName("魔法伤害")]
        MagicDamage,
        [InspectorName("真实伤害")]
        TrueDamage
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
        [InspectorName("攻击力")]
        AttackDamage,
        [InspectorName("法强")]
        AbilityPower,
        [InspectorName("普攻速")]
        AttackSpeed,
        [InspectorName("移速")]
        MovementSpeed,
        [InspectorName("护甲")]
        Armor,
        [InspectorName("魔抗")]
        MagicResist,
        [InspectorName("护甲穿透固定值")]
        ArmorPenetrationNum,
        [InspectorName("护甲穿透百分比")]
        ArmorPenetrationPercent,
        [InspectorName("法穿固定值")]
        MagicPenetrationNum,
        [InspectorName("法穿百分比")]
        MagicPenetrationPercent,
    }
    public enum BulletType
    {
        [InspectorName("追踪子弹")]
        TrackingBullet,
        [InspectorName("围绕持有者旋转子弹")]
        RotateOwner,
    }
}