using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Conditions
{
    [SkillDrawer(typeof(ConditionBase))]
    public class ConditionBaseEditor
    {
        public static void OnGUIDraw(ConditionBase conditionBase)
        {
            if (conditionBase != null)
            {
                if (conditionBase.ReverseResult)
                {
                    EditorGUILayout.LabelField("无条件禁止");
                }
                else
                {
                    EditorGUILayout.LabelField("无条件通过");
                }
            }
            ConditionBaseDraw(conditionBase);
        }

        public static void ConditionBaseDraw(ConditionBase conditionBase)
        {
            if (conditionBase != null)
            {
                conditionBase.ReverseResult = EditorGUILayout.Toggle("是否结果取反",conditionBase.ReverseResult);
            }
        }
    }
}