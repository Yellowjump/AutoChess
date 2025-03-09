using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.TargetPickers
{
    [SkillDrawer(typeof(TargetPickerNearest))]
    public class TargetPickerNearestEditor
    {
        public void OnGUIDraw(TargetPickerNearest targetPickerBase)
        {
            if (targetPickerBase != null)
            {
                targetPickerBase.TargetCamp = (CampType)EditorGUILayout.EnumPopup("目标阵营", targetPickerBase.TargetCamp);
                GUILayout.Label("武器长度mm:");
                SkillSystemDrawerCenter.DrawOneInstance(targetPickerBase.WeaponLength);
            }
        }
    }
}