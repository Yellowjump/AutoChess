using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.TargetPickers
{
    [SkillDrawer(typeof(TargetPickerRandomFromList))]
    public class TargetPickerRandomFromListEditor
    {
        public void OnGUIDraw(TargetPickerRandomFromList targetPickerBase)
        {
            if (targetPickerBase != null)
            {
                targetPickerBase.CurNumberType = (NumberCheckType)EditorGUILayout.EnumPopup("目标数量类型",targetPickerBase.CurNumberType);
                if (targetPickerBase.CurNumberType == NumberCheckType.FixedNumber)
                {
                    EditorGUILayout.LabelField("固定次数:");
                }
                else if (targetPickerBase.CurNumberType == NumberCheckType.ParentSkillContainSubItemNumber)
                {
                    EditorGUILayout.LabelField("目标子itemID：");
                }
                SkillSystemDrawerCenter.DrawOneInstance(targetPickerBase.ParamInt1);
                targetPickerBase.MeetNumber = EditorGUILayout.Toggle("是否强制满足数量", targetPickerBase.MeetNumber);
                var oldTargetPickerType = targetPickerBase.WorkTargetPicker.CurTargetPickerType;
                var newTargetPickerType = (TargetPickerType)EditorGUILayout.EnumPopup("目标选择",targetPickerBase.WorkTargetPicker.CurTargetPickerType);
                if (oldTargetPickerType != newTargetPickerType)
                {
                    targetPickerBase.WorkTargetPicker = SkillFactory.CreateTargetPicker(newTargetPickerType);
                }
                SkillSystemDrawerCenter.DrawOneInstance(targetPickerBase.WorkTargetPicker);
            }
        }
    }
}