using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandHuDun))]
    public class CommandHuDunEditor
    {
        public void OnGUIDraw(CommandHuDun commandHuDun)
        {
            if (commandHuDun != null)
            {
                EditorGUILayout.LabelField("创建护盾");
                commandHuDun.RemoveCurHudun = EditorGUILayout.Toggle("移除当前buff护盾",commandHuDun.RemoveCurHudun);
                if (commandHuDun.RemoveCurHudun)
                {
                    return;
                }
                commandHuDun.HudunChangeType = (AttributeChangeType)EditorGUILayout.EnumPopup("护盾计算类型", commandHuDun.HudunChangeType);
                if (commandHuDun.HudunChangeType == AttributeChangeType.FixNumAddAttrPercent)
                {
                    EditorGUILayout.LabelField("固定值");
                    SkillSystemDrawerCenter.DrawOneInstance(commandHuDun.ParamInt1);
                    EditorGUILayout.LabelField("属性枚举");
                    if (commandHuDun.ParamInt2.CurMatchTable == GenerateEnumDataTables.None)
                    {
                        commandHuDun.ParamInt2.CurMatchTable = (GenerateEnumDataTables)EditorGUILayout.EnumPopup("选择读表", commandHuDun.ParamInt2.CurMatchTable);
                        commandHuDun.ParamInt2.Value = (int)(AttributeType)EditorGUILayout.EnumPopup("加成属性", (AttributeType)commandHuDun.ParamInt2.Value);
                    }
                    else
                    {
                        SkillSystemDrawerCenter.DrawOneInstance(commandHuDun.ParamInt2);
                    }
                    EditorGUILayout.LabelField("属性百分比");
                    SkillSystemDrawerCenter.DrawOneInstance(commandHuDun.ParamInt3);
                }
                else if (commandHuDun.HudunChangeType == AttributeChangeType.ConstNum)
                {
                    EditorGUILayout.LabelField("护盾固定值");
                    SkillSystemDrawerCenter.DrawOneInstance(commandHuDun.ParamInt1);
                }
            }
        }
    }
}