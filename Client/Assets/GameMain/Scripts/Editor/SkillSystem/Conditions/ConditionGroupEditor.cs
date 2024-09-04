using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Conditions
{
    [SkillDrawer(typeof(ConditionGroup))]
    public class ConditionGroupEditor
    {
        public void OnGUIDraw(ConditionGroup conditionGroup)
        {
            if (conditionGroup == null) return;
            conditionGroup.CurLogicOperator =(LogicOperator) EditorGUILayout.EnumPopup("条件组类型",conditionGroup.CurLogicOperator);
            if (GUILayout.Button("添加条件"))
            {
                conditionGroup.ConditionList.Add(SkillFactory.CreateCondition(ConditionType.NoCondition));
            }
            if (conditionGroup.ConditionList != null && conditionGroup.ConditionList.Count != 0)
            {
                for (var conditionIndex = 0; conditionIndex < conditionGroup.ConditionList.Count; conditionIndex++)
                {
                    var oneCondition = conditionGroup.ConditionList[conditionIndex];
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        conditionGroup.ConditionList.Remove(oneCondition);
                    }
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    var oldConditionType = oneCondition.CurConditionType;
                    var newConditionType =
                        (ConditionType)EditorGUILayout.EnumPopup("触发条件", oneCondition.CurConditionType);
                    if (newConditionType != oldConditionType)
                    {
                        conditionGroup.ConditionList[conditionIndex] = SkillFactory.CreateCondition(newConditionType);
                    }
                    SkillSystemDrawerCenter.DrawOneInstance(oneCondition);
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(5);
                    EditorGUILayout.EndHorizontal();
                }
            }
            ConditionBaseEditor.ConditionBaseDraw(conditionGroup);
        }
    }
}