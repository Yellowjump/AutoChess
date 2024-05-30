using DataTable;
using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem
{
    [SkillDrawer(typeof(TableParamString))]
    public class TableParamStringEditor
    {
        public static void OnGUIDraw(TableParamString tableParamString)
        {
            if (tableParamString != null)
            {
                tableParamString.CurMatchTable = (GenerateEnumDataTables)EditorGUILayout.EnumPopup("选择读表", tableParamString.CurMatchTable);
                if (tableParamString.CurMatchTable == GenerateEnumDataTables.None)
                {
                    tableParamString.Value = EditorGUILayout.TextField("值：", tableParamString.Value);
                }
                else if (tableParamString.CurMatchTable == GenerateEnumDataTables.Skill)
                {
                    tableParamString.CurMatchPropertyIndex = (int)(DRSkillField)EditorGUILayout.EnumPopup("读取列", (DRSkillField)tableParamString.CurMatchPropertyIndex);
                }
                else if (tableParamString.CurMatchTable == GenerateEnumDataTables.Buff)
                {
                    tableParamString.CurMatchPropertyIndex = (int)(DRBuffField)EditorGUILayout.EnumPopup("读取列", (DRBuffField)tableParamString.CurMatchPropertyIndex);
                }
            }
        }
    }
}