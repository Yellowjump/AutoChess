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
            EditorGUILayout.LabelField("触发次数(0为不限制)");
            SkillSystemDrawerCenter.DrawOneInstance(conditionTimed.PassNumber);
            SkillSystemDrawerCenter.DrawOneInstance(conditionTimed.TimeIntervalMs);
            ConditionBaseEditor.ConditionBaseDraw(conditionTimed);
        }
    }
}