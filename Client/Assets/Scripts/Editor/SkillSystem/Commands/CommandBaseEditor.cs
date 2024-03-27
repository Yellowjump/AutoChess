using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandBase))]
    public class CommandBaseEditor
    {
        public static void OnGUIDraw(CommandBase commandBase)
        {
            if (commandBase != null)
            {
                EditorGUILayout.LabelField("这是一条空命令，开始摆烂");
            }
        }
    }
}