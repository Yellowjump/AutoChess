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
                if (commandCauseDamage.CurDamageComputeType == DamageComputeType.RealDamage)
                {
                    //真实伤害，只用paramInt1
                    SkillSystemDrawerCenter.DrawOneInstance(commandCauseDamage.ParamInt1);
                }
                else if (commandCauseDamage.CurDamageComputeType == DamageComputeType.CommonDamage)
                {
                    //通用伤害计算
                }
            }
        }
    }
}