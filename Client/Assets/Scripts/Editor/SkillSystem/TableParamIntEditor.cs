using DataTable;
using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem
{
    [SkillDrawer(typeof(TableParamInt))]
    public class TableParamIntEditor
    {
        public static void OnGUIDraw(TableParamInt tableParamInt)
        {
            if (tableParamInt != null)
            {
                tableParamInt.CurMatchTable = (GenerateEnumDataTables)EditorGUILayout.EnumPopup("选择读表", tableParamInt.CurMatchTable);
                if (tableParamInt.CurMatchTable == GenerateEnumDataTables.None)
                {
                    tableParamInt.Value = EditorGUILayout.IntField("值：", tableParamInt.Value);
                }
                else if (tableParamInt.CurMatchTable == GenerateEnumDataTables.Skill)
                {
                    tableParamInt.CurMatchPropertyIndex = (int)(DRSkillField)EditorGUILayout.EnumPopup("读取列", (DRSkillField)tableParamInt.CurMatchPropertyIndex);
                }
            }
        }
    }
}