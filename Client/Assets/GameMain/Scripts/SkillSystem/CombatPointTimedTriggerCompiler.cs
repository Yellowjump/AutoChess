using Entity;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    // Converts the common timed point rules to value-only ECS timer state.
    // Unsupported condition/picker/command combinations keep the old path.
    internal static class CombatPointTimedTriggerCompiler
    {
        public static bool TryConfigure(TriggerList source, EntityPosPoint point,
            Skill parentSkill)
        {
            if (source?.CurTriggerList == null || point == null ||
                point.CombatHandle == 0 || parentSkill == null) return false;

            OneTrigger timedTrigger = null;
            ConditionTimed timedCondition = null;
            bool presentationOnly = false;
            foreach (OneTrigger trigger in source.CurTriggerList)
            {
                if (trigger.CurTriggerType != TriggerType.PerTick) continue;
                if (trigger.CurCommandList == null ||
                    trigger.CurCommandList.Count == 0)
                {
                    // Only skip the template's deterministic, effect-free
                    // distance probe. Other empty triggers may consume RNG.
                    if (trigger.CurTargetPicker?.CurTargetPickerType !=
                            TargetPickerType.OwnerDistance ||
                        !TryGetTimedCondition(trigger.CurCondition, out _))
                        return false;
                    continue;
                }
                if (timedTrigger != null ||
                    !TryGetTimedCondition(trigger.CurCondition, out timedCondition))
                    return false;
                TargetPickerType picker =
                    trigger.CurTargetPicker?.CurTargetPickerType ??
                    TargetPickerType.NoTarget;
                presentationOnly = picker == TargetPickerType.TriggerOwner;
                if (!presentationOnly &&
                    (picker != TargetPickerType.Arg ||
                     point.Target is not EntityQizi)) return false;
                foreach (CommandBase command in trigger.CurCommandList)
                {
                    if (presentationOnly)
                    {
                        if (command is CommandCreateSfx sfx && sfx.SfxID != null)
                            continue;
                        if (command is CommandPlayAudio audio &&
                            audio.AudioID != null) continue;
                        return false;
                    }
                    else if (command is not CommandCreateBullet bullet ||
                             bullet.CurBulletID == null || bullet.ParamInt1 == null ||
                             bullet.PosOffset == null || bullet.BulletTrigger == null)
                    {
                        return false;
                    }
                }
                timedTrigger = trigger;
            }
            if (timedTrigger == null || timedCondition == null ||
                timedCondition.TimeIntervalMs.Value <= 0) return false;

            if (presentationOnly)
            {
                foreach (CommandBase command in timedTrigger.CurCommandList)
                {
                    if (command is CommandCreateSfx sfx)
                        CombatPosPointBridge.AddTimedPresentationRule(
                            point.CombatHandle, CombatPointPresentationKind.Sfx,
                            sfx.SfxID.Value, sfx.CreateOrRemove);
                    else if (command is CommandPlayAudio audio)
                        CombatPosPointBridge.AddTimedPresentationRule(
                            point.CombatHandle, CombatPointPresentationKind.Audio,
                            audio.AudioID.Value, audio.CreateOrRemove);
                }
                CombatPosPointBridge.ConfigureTimedPresentation(point.CombatHandle,
                    timedCondition.TimeIntervalMs.Value,
                    timedCondition.PassNumber.Value,
                    timedTrigger.CurCommandList.Count);
            }
            else
            {
                point.ConfigureTimedProjectiles(timedTrigger.CurCommandList,
                    parentSkill);
                CombatPosPointBridge.ConfigureTimedProjectiles(point.CombatHandle,
                    timedCondition.TimeIntervalMs.Value,
                    timedCondition.PassNumber.Value,
                    timedTrigger.CurCommandList.Count);
            }
            return true;
        }

        private static bool TryGetTimedCondition(ConditionBase condition,
            out ConditionTimed timed)
        {
            timed = null;
            if (condition == null || condition.ReverseResult) return false;
            while (condition is ConditionGroup group)
            {
                if (group.ConditionList == null || group.ConditionList.Count != 1)
                    return false;
                condition = group.ConditionList[0];
                if (condition == null || condition.ReverseResult) return false;
            }
            timed = condition as ConditionTimed;
            return timed != null;
        }
    }
}
