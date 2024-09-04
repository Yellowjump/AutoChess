using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.TargetPickers
{
    [SkillDrawer(typeof(TargetPickerBase))]
    public class TargetPickerBaseEditor
    {
        public void OnGUIDraw(TargetPickerBase targetPickerBase)
        {
            if (targetPickerBase != null)
            {
                EditorGUILayout.LabelField("不选择任何目标");
            }
        }
    }
}