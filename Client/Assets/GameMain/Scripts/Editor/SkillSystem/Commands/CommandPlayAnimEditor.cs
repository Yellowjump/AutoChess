using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandPlayAnim))]
    public class CommandPlayAnimEditor
    {
        public void OnGUIDraw(CommandPlayAnim commandPlayAnim)
        {
            if (commandPlayAnim != null)
            {
                EditorGUILayout.LabelField("播放动作");
                SkillSystemDrawerCenter.DrawOneInstance(commandPlayAnim.AnimAssetID);
            }
        }
    }
}