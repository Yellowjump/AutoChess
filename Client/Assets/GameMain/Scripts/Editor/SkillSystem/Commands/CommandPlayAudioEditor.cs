using DataTable;
using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandLightningEffect))]
    public class CommandLightningEffectEditor
    {
        public void OnGUIDraw(CommandLightningEffect command)
        {
            command.Mode = (CommandLightningEffect.Effect)EditorGUILayout.EnumPopup("雷印效果", command.Mode);
            EditorGUILayout.LabelField(command.Mode == CommandLightningEffect.Effect.Discharge ? "伤害（攻击力百分比）" : "施加层数");
            SkillSystemDrawerCenter.DrawOneInstance(command.Param);
        }
    }

    [SkillDrawer(typeof(CommandWeaknessEffect))]
    public class CommandWeaknessEffectEditor
    {
        public void OnGUIDraw(CommandWeaknessEffect command)
        {
            command.Mode = (CommandWeaknessEffect.Effect)EditorGUILayout.EnumPopup("效果", command.Mode);
            if (command.Mode is CommandWeaknessEffect.Effect.Strike or CommandWeaknessEffect.Effect.Retaliate)
            {
                EditorGUILayout.LabelField("伤害百分比");
                SkillSystemDrawerCenter.DrawOneInstance(command.Param);
            }
        }
    }

    [SkillDrawer(typeof(CommandBurnEffect))]
    public class CommandBurnEffectEditor
    {
        public void OnGUIDraw(CommandBurnEffect command)
        {
            command.Mode = (CommandBurnEffect.Effect)EditorGUILayout.EnumPopup("燃烧套装效果", command.Mode);
            string label = command.Mode switch
            {
                CommandBurnEffect.Effect.Feed => "触发间隔（毫秒）",
                CommandBurnEffect.Effect.Detonate => "每层伤害（攻击力百分比）",
                _ => "施加层数"
            };
            EditorGUILayout.LabelField(label);
            SkillSystemDrawerCenter.DrawOneInstance(command.Param);
        }
    }

    [SkillDrawer(typeof(CommandBleedEffect))]
    public class CommandBleedEffectEditor
    {
        public void OnGUIDraw(CommandBleedEffect command)
        {
            command.Mode = (CommandBleedEffect.Effect)EditorGUILayout.EnumPopup("流血套装效果", command.Mode);
            EditorGUILayout.LabelField(command.Mode == CommandBleedEffect.Effect.ApplyBleed ? "Buff ID" : "百分比");
            SkillSystemDrawerCenter.DrawOneInstance(command.Param);
        }
    }

    [SkillDrawer(typeof(CommandShieldDamageModifier))]
    public class CommandShieldDamageModifierEditor
    {
        public void OnGUIDraw(CommandShieldDamageModifier command)
        {
            EditorGUILayout.LabelField("伤害调整百分比（负数减伤）");
            SkillSystemDrawerCenter.DrawOneInstance(command.Percent);
        }
    }

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
