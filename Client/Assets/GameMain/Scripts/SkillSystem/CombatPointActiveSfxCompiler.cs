using Zizouqi.CombatDots;

namespace SkillSystem
{
    // Compile the point OnActive rules that only emit presentation intents.
    // The timed and hit rules keep their own runtime state and are unaffected.
    internal static class CombatPointActiveSfxCompiler
    {
        public static bool TryQueue(TriggerList triggers, int pointHandle)
        {
            if (triggers?.CurTriggerList == null || pointHandle == 0)
                return false;

            bool found = false;
            foreach (OneTrigger trigger in triggers.CurTriggerList)
            {
                if (trigger.CurTriggerType != TriggerType.OnActive) continue;
                if (trigger.CurCondition == null ||
                    trigger.CurCondition.CurConditionType != ConditionType.NoCondition ||
                    trigger.CurCondition.ReverseResult ||
                    trigger.CurTargetPicker == null ||
                    trigger.CurTargetPicker.CurTargetPickerType !=
                        TargetPickerType.TriggerOwner ||
                    trigger.CurCommandList == null)
                    return false;
                foreach (CommandBase command in trigger.CurCommandList)
                {
                    if (command is CommandCreateSfx sfx && sfx.SfxID != null)
                        continue;
                    if (command is CommandPlayAudio audio && audio.AudioID != null)
                        continue;
                    return false;
                }
                found = true;
            }
            if (!found) return false;

            foreach (OneTrigger trigger in triggers.CurTriggerList)
            {
                if (trigger.CurTriggerType != TriggerType.OnActive) continue;
                foreach (CommandBase command in trigger.CurCommandList)
                {
                    if (command is CommandCreateSfx sfx)
                        CombatPosPointBridge.AddPresentationRule(pointHandle,
                            (int)TriggerType.OnActive,
                            CombatPointPresentationKind.Sfx, sfx.SfxID.Value,
                            sfx.CreateOrRemove);
                    else if (command is CommandPlayAudio audio)
                        CombatPosPointBridge.AddPresentationRule(pointHandle,
                            (int)TriggerType.OnActive,
                            CombatPointPresentationKind.Audio, audio.AudioID.Value,
                            audio.CreateOrRemove);
                }
            }
            CombatPosPointBridge.QueueTrigger(pointHandle, (int)TriggerType.OnActive);
            return true;
        }
    }
}
