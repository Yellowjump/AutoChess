using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandChangeAttribute))]
    public class CommandChangeAttributeEditor
    {
        public void OnGUIDraw(CommandChangeAttribute commandChangeAttribute)
        {
            if (commandChangeAttribute != null)
            {
                EditorGUILayout.LabelField("修改属性");
                if (commandChangeAttribute.TargetAttrType.CurMatchTable == GenerateEnumDataTables.None)
                {
                    commandChangeAttribute.TargetAttrType.CurMatchTable = (GenerateEnumDataTables)EditorGUILayout.EnumPopup("选择读表", commandChangeAttribute.TargetAttrType.CurMatchTable);
                    commandChangeAttribute.TargetAttrType.Value = (int)(AttributeType)EditorGUILayout.EnumPopup("目标属性", (AttributeType)commandChangeAttribute.TargetAttrType.Value);
                }
                else
                {
                    SkillSystemDrawerCenter.DrawOneInstance(commandChangeAttribute.TargetAttrType);
                }
                commandChangeAttribute.AddOrReduce = EditorGUILayout.Toggle("增加或减少", commandChangeAttribute.AddOrReduce);
                commandChangeAttribute.CurAttributeChangeType = (AttributeChangeType)EditorGUILayout.EnumPopup("修改方式", commandChangeAttribute.CurAttributeChangeType);
                SkillSystemDrawerCenter.DrawOneInstance(commandChangeAttribute.ParamInt1);
            }
        }
    }
}