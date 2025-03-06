using DataTable;
using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem
{
    [SkillDrawer(typeof(TableParamVector3))]
    public class TableParamVector3Editor
    {
        public static void OnGUIDraw(TableParamVector3 tableParamVector3)
        {
            if (tableParamVector3 != null)
            {
                tableParamVector3.CurMatchTable = (GenerateEnumDataTables)EditorGUILayout.EnumPopup("选择读表", tableParamVector3.CurMatchTable);
                if (tableParamVector3.CurMatchTable == GenerateEnumDataTables.None)
                {
                    tableParamVector3.Value = EditorGUILayout.Vector3Field("值：", tableParamVector3.Value);
                }
                else if (tableParamVector3.CurMatchTable == GenerateEnumDataTables.Skill)
                {
                    tableParamVector3.CurMatchPropertyIndex = (int)(DRSkillField)EditorGUILayout.EnumPopup("读取列", (DRSkillField)tableParamVector3.CurMatchPropertyIndex);
                }
                else if (tableParamVector3.CurMatchTable == GenerateEnumDataTables.Buff)
                {
                    tableParamVector3.CurMatchPropertyIndex = (int)(DRBuffField)EditorGUILayout.EnumPopup("读取列", (DRBuffField)tableParamVector3.CurMatchPropertyIndex);
                }
            }
        }
    }
}