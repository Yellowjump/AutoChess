using SkillSystem;
using UnityEditor;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandCauseDamage))]
    public class CommandCauseDamageEditor
    {
        public void OnGUIDraw(CommandCauseDamage commandCauseDamage)
        {
            if (commandCauseDamage != null)
            {
                EditorGUILayout.LabelField("造成伤害");
                commandCauseDamage.CurDamageComputeType = (DamageComputeType)EditorGUILayout.EnumPopup("伤害计算类型", commandCauseDamage.CurDamageComputeType);
                commandCauseDamage.CurDamageType = (DamageType)EditorGUILayout.EnumPopup("伤害类型", commandCauseDamage.CurDamageType);
                if (commandCauseDamage.CurDamageComputeType == DamageComputeType.FixNumAddAttrPercent)
                {
                    //固定伤害加属性百分比，paramInt1 是固定伤害。paramInt2 是 属性枚举，paramInt3 是 百分比
                    EditorGUILayout.LabelField("固定伤害");
                    SkillSystemDrawerCenter.DrawOneInstance(commandCauseDamage.ParamInt1);
                    EditorGUILayout.LabelField("属性枚举");
                    if (commandCauseDamage.ParamInt2.CurMatchTable == GenerateEnumDataTables.None)
                    {
                        commandCauseDamage.ParamInt2.CurMatchTable = (GenerateEnumDataTables)EditorGUILayout.EnumPopup("选择读表", commandCauseDamage.ParamInt2.CurMatchTable);
                        commandCauseDamage.ParamInt2.Value = (int)(AttributeType)EditorGUILayout.EnumPopup("加成属性", (AttributeType)commandCauseDamage.ParamInt2.Value);
                    }
                    else
                    {
                        SkillSystemDrawerCenter.DrawOneInstance(commandCauseDamage.ParamInt2);
                    }
                    EditorGUILayout.LabelField("属性百分比");
                    SkillSystemDrawerCenter.DrawOneInstance(commandCauseDamage.ParamInt3);
                }
                else if (commandCauseDamage.CurDamageComputeType == DamageComputeType.NormalDamage)
                {
                    //伤害计算
                    commandCauseDamage.CurDamageType = DamageType.PhysicalDamage;//普攻固定是物理攻击
                }
            }
        }
    }
}