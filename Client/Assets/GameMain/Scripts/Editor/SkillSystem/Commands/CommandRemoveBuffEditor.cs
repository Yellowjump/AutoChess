using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandRemoveBuff))]
    public class CommandRemoveBuffEditor
    {
        public void OnGUIDraw(CommandRemoveBuff commandRemoveBuff)
        {
            if (commandRemoveBuff != null)
            {
                commandRemoveBuff.RemoveCurBuff = GUILayout.Toggle(commandRemoveBuff.RemoveCurBuff, "移除当前所在buff");
                if (commandRemoveBuff.RemoveCurBuff == false)
                {
                    GUILayout.Label("移除目标buffID");
                    SkillSystemDrawerCenter.DrawOneInstance(commandRemoveBuff.BuffID);
                }
                
            }
        }
    }
}