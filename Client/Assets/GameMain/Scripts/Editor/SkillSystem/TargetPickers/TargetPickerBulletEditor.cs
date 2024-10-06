using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.TargetPickers
{
    [SkillDrawer(typeof(TargetPickerBullet))]
    public class TargetPickerBulletEditor
    {
        public void OnGUIDraw(TargetPickerBullet targetPickerBase)
        {
            if (targetPickerBase != null)
            {
                
                GUILayout.Label("选取子弹ID:");
                SkillSystemDrawerCenter.DrawOneInstance(targetPickerBase.BulletID);
                targetPickerBase.SameCaster = EditorGUILayout.Toggle("取同一释放者", targetPickerBase.SameCaster);
            }
        }
    }
}