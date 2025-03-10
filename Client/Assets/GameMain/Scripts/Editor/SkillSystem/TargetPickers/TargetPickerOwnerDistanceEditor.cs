using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.TargetPickers
{
    [SkillDrawer(typeof(TargetPickerOwnerDistance))]
    public class TargetPickerOwnerDistanceEditor
    {
        public void OnGUIDraw(TargetPickerOwnerDistance targetPickerBase)
        {
            if (targetPickerBase != null)
            {
                targetPickerBase.LengthUseWeapon = EditorGUILayout.Toggle("是否使用道具长度", targetPickerBase.LengthUseWeapon);
                if (!targetPickerBase.LengthUseWeapon)
                {
                    GUILayout.Label("武器长度mm:");
                    SkillSystemDrawerCenter.DrawOneInstance(targetPickerBase.WeaponLength);
                }
                targetPickerBase.TargetCamp = (CampType)EditorGUILayout.EnumPopup("目标阵营", targetPickerBase.TargetCamp);
            }
        }
    }
}