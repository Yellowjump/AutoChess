using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandCreateSfx))]
    public class CommandCreateSfxEditor
    {
        public void OnGUIDraw(CommandCreateSfx commandCreateSfx)
        {
            if (commandCreateSfx != null)
            {
                EditorGUILayout.LabelField(commandCreateSfx.CreateOrRemove?"创建特效":"移除特效");
                commandCreateSfx.CreateOrRemove = GUILayout.Toggle(commandCreateSfx.CreateOrRemove, "创建特效Or移除特效");
                SkillSystemDrawerCenter.DrawOneInstance(commandCreateSfx.SfxID);
            }
        }
    }
}