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
            conditionTimed.PassOnly = EditorGUILayout.Toggle("是否只触发一次", conditionTimed.PassOnly);
            SkillSystemDrawerCenter.DrawOneInstance(conditionTimed.TimeIntervalMs);
            ConditionBaseEditor.ConditionBaseDraw(conditionTimed);
        }
    }
}