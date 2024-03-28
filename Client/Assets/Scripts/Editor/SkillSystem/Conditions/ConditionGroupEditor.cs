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
                    var oldConditionType = oneCondition.CurConditionType;
                    var newConditionType =
                        (ConditionType)EditorGUILayout.EnumPopup("触发条件", oneCondition.CurConditionType);
                    if (newConditionType != oldConditionType)
                    {
                        conditionGroup.ConditionList[conditionIndex] = SkillFactory.CreateCondition(newConditionType);
                    }
                    SkillSystemDrawerCenter.DrawOneInstance(oneCondition);
                }
            }
            ConditionBaseEditor.ConditionBaseDraw(conditionGroup);
        }
    }
}