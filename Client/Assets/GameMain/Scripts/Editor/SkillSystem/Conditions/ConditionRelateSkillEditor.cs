using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Conditions
{
    [SkillDrawer(typeof(ConditionRelateSkill))]
    public class ConditionRelateSkillEditor
    {
        public void OnGUIDraw(ConditionRelateSkill conditionRelateSkill)
        {
            if (conditionRelateSkill == null) return;
            conditionRelateSkill.SkillFrom = (ConditionRelateSkillFrom)EditorGUILayout.EnumPopup("目标技能：", conditionRelateSkill.SkillFrom);
            conditionRelateSkill.CheckType = (ConditionRelateSkillCheckType)EditorGUILayout.EnumPopup("检测类型：", conditionRelateSkill.CheckType);
            if (conditionRelateSkill.CheckType == ConditionRelateSkillCheckType.SkillType)
            {
                GUILayout.Label("技能类型");
                SkillSystemDrawerCenter.DrawOneInstance(conditionRelateSkill.ParamInt1);
            }
        }
    }
}