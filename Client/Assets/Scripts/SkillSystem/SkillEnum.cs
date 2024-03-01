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
        OnActive,
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
    }

    public enum ConditionType
    {
        /// <summary>
        /// 无条件通过
        /// </summary>
        NoCondition,
        /// <summary>
        /// 条件组
        /// </summary>
        ConditionGroup,
        /// <summary>
        /// 概率
        /// </summary>
        Percentage,
    }

    public enum TargetPickerType
    {
        /// <summary>
        /// 当前普工目标
        /// </summary>
        CurNormalTarget,
        /// <summary>
        /// 最近的目标（能选中的）
        /// </summary>
        Nearest,
    }

    public enum CommandType
    {
        /// <summary>
        /// 造成伤害
        /// </summary>
        CauseDamage,
    }
}