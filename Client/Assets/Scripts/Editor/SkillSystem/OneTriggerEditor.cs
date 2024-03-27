using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem
{
    [SkillDrawer(typeof(OneTrigger))]
    public class OneTriggerEditor
    {
        public static void OnGUIDraw(OneTrigger oneTrigger)
        {
            if (oneTrigger !=null)
            {
                oneTrigger.CurTriggerType =(TriggerType)EditorGUILayout.EnumPopup("触发类型",oneTrigger.CurTriggerType);
                var oldConditionType = oneTrigger.CurCondition.CurConditionType;
                var newConditionType = (ConditionType)EditorGUILayout.EnumPopup("触发条件",oneTrigger.CurCondition.CurConditionType);
                if (newConditionType != oldConditionType)
                {
                    oneTrigger.CurCondition = SkillFactory.CreateCondition(newConditionType);
                }
                SkillSystemDrawer.DrawOneInstance(oneTrigger.CurCondition);
                var oldTargetPickerType = oneTrigger.CurTargetPicker.CurTargetPickerType;
                var newTargetPickerType = (TargetPickerType)EditorGUILayout.EnumPopup("目标选择",oneTrigger.CurTargetPicker.CurTargetPickerType);
                if (oldTargetPickerType != newTargetPickerType)
                {
                    oneTrigger.CurTargetPicker = SkillFactory.CreateTargetPicker(newTargetPickerType);
                }
                SkillSystemDrawer.DrawOneInstance(oneTrigger.CurTargetPicker);
                if (GUILayout.Button("添加Command"))
                {
                    oneTrigger.CurCommandList.Add(SkillFactory.CreateCommand(CommandType.CauseDamage));
                }

                if (oneTrigger.CurCommandList != null && oneTrigger.CurCommandList.Count != 0)
                {
                    foreach (var oneCommand in oneTrigger.CurCommandList)
                    {
                        SkillSystemDrawer.DrawOneInstance(oneCommand);
                    }
                }
                
            }
        }
    }
}