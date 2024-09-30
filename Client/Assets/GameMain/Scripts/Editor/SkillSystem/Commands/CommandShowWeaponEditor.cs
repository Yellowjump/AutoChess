using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandShowWeapon))]
    public class CommandShowWeaponEditor
    {
        public void OnGUIDraw(CommandShowWeapon commandShowWeapon)
        {
            if (commandShowWeapon != null)
            {
                if (commandShowWeapon.ShowOrHidden)
                {
                    EditorGUILayout.LabelField("显示武器");
                }
                else
                {
                    EditorGUILayout.LabelField("去除该技能所有武器");
                    commandShowWeapon.ShowOrHidden = EditorGUILayout.Toggle("显示或移除武器(当前移除)",commandShowWeapon.ShowOrHidden);
                    return;
                }
                commandShowWeapon.ShowOrHidden = EditorGUILayout.Toggle("显示武器(当前显示)",commandShowWeapon.ShowOrHidden);
                commandShowWeapon.ShowHandleType = (WeaponHandleType)EditorGUILayout.EnumPopup("显示位置", commandShowWeapon.ShowHandleType);
                if (!commandShowWeapon.ShowItemWeapon)
                {
                    commandShowWeapon.ShowItemWeapon = EditorGUILayout.Toggle("显示指定道具武器",commandShowWeapon.ShowItemWeapon);
                    SkillSystemDrawerCenter.DrawOneInstance(commandShowWeapon.ShowWeaponItemID);
                }
                else
                {
                    commandShowWeapon.ShowItemWeapon = EditorGUILayout.Toggle("显示该技能对应道具武器",commandShowWeapon.ShowItemWeapon);
                }
            }
        }
    }
}