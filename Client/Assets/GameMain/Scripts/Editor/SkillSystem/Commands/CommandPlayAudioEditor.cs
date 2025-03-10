using DataTable;
using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandPlayAudio))]
    public class CommandPlayAudioEditor
    {
        public void OnGUIDraw(CommandPlayAudio commandPlayAudio)
        {
            if (commandPlayAudio != null)
            {
                EditorGUILayout.LabelField(commandPlayAudio.CreateOrRemove?"播放音效":"移除同buff上的音效");
                commandPlayAudio.CreateOrRemove = GUILayout.Toggle(commandPlayAudio.CreateOrRemove, "播放Or移除");
                if (commandPlayAudio.CreateOrRemove)
                {
                    if (commandPlayAudio.AudioID.CurMatchTable == GenerateEnumDataTables.None)
                    {
                        commandPlayAudio.AudioID.CurMatchTable = (GenerateEnumDataTables)EditorGUILayout.EnumPopup("选择读表", commandPlayAudio.AudioID.CurMatchTable);
                        EnumSound audioID = (EnumSound)commandPlayAudio.AudioID.Value;
                        audioID = (EnumSound)EditorGUILayout.EnumPopup("sound表中枚举", audioID);
                        commandPlayAudio.AudioID.Value = (int)audioID;
                    }
                    else
                    {
                        SkillSystemDrawerCenter.DrawOneInstance(commandPlayAudio.AudioID);
                    }
                }
            }
        }
    }
}