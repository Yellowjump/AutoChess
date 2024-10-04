using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Conditions
{
    [SkillDrawer(typeof(ConditionRelateItem))]
    public class ConditionRelateItemEditor
    {
        public void OnGUIDraw(ConditionRelateItem conditionRelateItem)
        {
            if (conditionRelateItem == null) return;
            conditionRelateItem.ItemFrom = (ConditionRelateItemFrom)EditorGUILayout.EnumPopup("目标道具：", conditionRelateItem.ItemFrom);
            conditionRelateItem.CheckType = (ConditionRelateItemCheckType)EditorGUILayout.EnumPopup("检测类型：", conditionRelateItem.CheckType);
            if (conditionRelateItem.CheckType == ConditionRelateItemCheckType.ContainItem)
            {
                GUILayout.Label("子道具ID");
                SkillSystemDrawerCenter.DrawOneInstance(conditionRelateItem.ParamInt1);
            }
        }
    }
}