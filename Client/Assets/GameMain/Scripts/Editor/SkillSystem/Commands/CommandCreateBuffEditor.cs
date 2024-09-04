using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandCreateBuff))]
    public class CommandCreateBuffEditor
    {
        public void OnGUIDraw(CommandCreateBuff commandCreateBuff)
        {
            if (commandCreateBuff != null)
            {
                EditorGUILayout.LabelField("添加buff");
                commandCreateBuff.UseTemplateBuff = GUILayout.Toggle(commandCreateBuff.UseTemplateBuff,"是否使用buff表中的buff");
                if (commandCreateBuff.UseTemplateBuff)
                {
                    SkillSystemDrawerCenter.DrawOneInstance(commandCreateBuff.BuffID);
                }
                else
                {
                    if (commandCreateBuff.TemporaryBuff == null)
                    {
                        commandCreateBuff.TemporaryBuff = SkillFactory.CreateNewBuff();
                    }
                    SkillSystemDrawerCenter.DrawOneInstance(commandCreateBuff.TemporaryBuff);
                }
            }
        }
    }
}