using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandRemoveBullet))]
    public class CommandRemoveBulletEditor
    {
        public void OnGUIDraw(CommandRemoveBullet commandRemoveBullet)
        {
            if (commandRemoveBullet != null)
            {
                commandRemoveBullet.RemoveCurBullet = GUILayout.Toggle(commandRemoveBullet.RemoveCurBullet, "移除当前所在子弹");
                if (commandRemoveBullet.RemoveCurBullet == false)
                {
                    GUILayout.Label("移除目标子弹");
                }
            }
        }
    }
}