using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Conditions
{
    [SkillDrawer(typeof(ConditionTimed))]
    public class ConditionTimedEditor
    {
        public void OnGUIDraw(ConditionTimed conditionTimed)
        {
            if (conditionTimed == null) return;
            SkillSystemDrawerCenter.DrawOneInstance(conditionTimed.TimeIntervalMs);
            ConditionBaseEditor.ConditionBaseDraw(conditionTimed);
        }
    }
}