using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.TargetPickers
{
    [SkillDrawer(typeof(TargetPickerRelatedDamageTarget))]
    public class TargetPickerRelatedDamageTargetEditor
    {
        public void OnGUIDraw(TargetPickerRelatedDamageTarget targetPickerBase)
        {
            if (targetPickerBase != null)
            {
                targetPickerBase.CurDamageDataTargetType = (DamageDataTargetType)EditorGUILayout.EnumPopup("具体目标",targetPickerBase.CurDamageDataTargetType);
            }
        }
    }
}