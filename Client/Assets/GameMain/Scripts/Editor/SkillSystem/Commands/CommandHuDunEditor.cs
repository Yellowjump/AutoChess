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
                EditorGUILayout.LabelField("护盾固定值");
                SkillSystemDrawerCenter.DrawOneInstance(commandHuDun.ParamInt1);
                
            }
        }
    }
}