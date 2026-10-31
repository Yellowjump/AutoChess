using System;
using System.Collections.Generic;
using GameFramework;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    internal static class CombatConditionCompiler
    {
        public static void Compile(ref int handle, ConditionBase root)
        {
            var nodes = new List<CombatConditionNode>();
            Append(root, nodes);
            CombatConditionProgramBridge.Create(ref handle, nodes);
        }

        public static CombatConditionContext CreateContext(OneTrigger trigger, object arg, float deltaMs)
        {
            Skill source = trigger.ParentTriggerList?.ParentSkill;
            Skill argSkill = arg as Skill;
            return new CombatConditionContext
            {
                HasSourceSkill = source != null,
                HasArgSkill = argSkill != null,
                SourceSkillType = source != null ? (int)source.CurSkillType : 0,
                ArgSkillType = argSkill != null ? (int)argSkill.CurSkillType : 0,
                SourceItemId = source != null ? source.FromItemID : 0,
                ArgItemId = argSkill != null ? argSkill.FromItemID : 0,
                DeltaMs = deltaMs
            };
        }

        public static int RollPercent()
        {
            return Utility.Random.GetRandom(100);
        }

        private static void Append(ConditionBase condition, List<CombatConditionNode> nodes)
        {
            int index = nodes.Count;
            var node = new CombatConditionNode { Reverse = condition.ReverseResult };
            switch (condition)
            {
                case ConditionGroup group:
                    node.Kind = CombatConditionKind.Group;
                    node.Logic = (byte)group.CurLogicOperator;
                    nodes.Add(node);
                    if (group.ConditionList != null)
                    {
                        foreach (ConditionBase child in group.ConditionList)
                        {
                            Append(child, nodes);
                        }
                    }
                    node.EndIndex = nodes.Count;
                    nodes[index] = node;
                    return;
                case ConditionPercent percent:
                    node.Kind = CombatConditionKind.Percent;
                    node.Param1 = percent.PercentTarget.Value;
                    break;
                case ConditionTimed timed:
                    node.Kind = CombatConditionKind.Timed;
                    node.Param1 = timed.TimeIntervalMs.Value;
                    node.Param2 = timed.PassNumber.Value;
                    break;
                case ConditionRelateItem item:
                    node.Kind = CombatConditionKind.RelateItem;
                    node.Source = (int)item.ItemFrom;
                    node.CheckType = (int)item.CheckType;
                    node.Param1 = item.ParamInt1.Value;
                    node.Param2 = item.ParamInt2.Value;
                    break;
                case ConditionRelateSkill skill:
                    node.Kind = CombatConditionKind.RelateSkill;
                    node.Source = (int)skill.SkillFrom;
                    node.CheckType = (int)skill.CheckType;
                    node.Param1 = skill.ParamInt1.Value;
                    node.Param2 = skill.ParamInt2.Value;
                    break;
                case ConditionBase _:
                    node.Kind = CombatConditionKind.Always;
                    break;
                default:
                    throw new ArgumentNullException(nameof(condition));
            }
            node.EndIndex = index + 1;
            nodes.Add(node);
        }
    }
}
