using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.TargetPickers
{
    [SkillDrawer(typeof(TargetPickerOwnerDirection))]
    public class TargetPickerOwnerDirectionEditor
    {
        public void OnGUIDraw(TargetPickerOwnerDirection targetPickerBase)
        {
            if (targetPickerBase != null)
            {
                targetPickerBase.LengthUseWeapon = EditorGUILayout.Toggle("是否使用道具长度", targetPickerBase.LengthUseWeapon);
                if (!targetPickerBase.LengthUseWeapon)
                {
                    GUILayout.Label("武器长度mm:");
                    SkillSystemDrawerCenter.DrawOneInstance(targetPickerBase.WeaponLength);
                }
                GUILayout.Label("选择范围角度:");
                SkillSystemDrawerCenter.DrawOneInstance(targetPickerBase.ValidAngle);
            }
        }
    }
}