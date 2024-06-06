using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandCreateBullet))]
    public class CommandCreateBulletEditor
    {
        public void OnGUIDraw(CommandCreateBullet commandCreateBullet)
        {
            if (commandCreateBullet != null)
            {
                EditorGUILayout.LabelField("创建子弹");
                EditorGUILayout.LabelField("子弹ID");
                SkillSystemDrawerCenter.DrawOneInstance(commandCreateBullet.CurBulletID);
                EditorGUILayout.LabelField("子弹上的触发器");
                SkillSystemDrawerCenter.DrawOneInstance(commandCreateBullet.BulletTrigger);
            }
        }
    }
}