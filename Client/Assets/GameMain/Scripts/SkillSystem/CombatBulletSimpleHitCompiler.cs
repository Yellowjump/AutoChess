using Zizouqi.CombatDots;

namespace SkillSystem
{
    // Compile the frequent hit sequence to one immutable projectile component.
    // The template stays available for persistence; no OnHit OneTrigger clone is needed.
    internal static class CombatBulletSimpleHitCompiler
    {
        public static bool TryBuild(TriggerList source,
            out CombatProjectileSimpleHit rule)
        {
            rule = default;
            if (source?.CurTriggerList == null) return false;

            OneTrigger hit = null;
            OneTrigger active = null;
            foreach (OneTrigger trigger in source.CurTriggerList)
            {
                if (trigger.CurTriggerType == TriggerType.OnActive)
                {
                    if (active != null || trigger.CurCondition == null ||
                        trigger.CurCondition.CurConditionType !=
                            ConditionType.NoCondition ||
                        trigger.CurCondition.ReverseResult ||
                        trigger.CurTargetPicker?.CurTargetPickerType !=
                            TargetPickerType.TriggerOwner ||
                        trigger.CurCommandList?.Count != 1 ||
                        trigger.CurCommandList[0] is not CommandPlayAudio audio ||
                        !audio.CreateOrRemove || audio.AudioID == null)
                        return false;
                    active = trigger;
                    continue;
                }
                if (trigger.CurTriggerType != TriggerType.OnBulletHitTarget)
                    return false;
                if (hit != null || trigger.CurCondition == null ||
                    trigger.CurCondition.CurConditionType != ConditionType.NoCondition ||
                    trigger.CurCondition.ReverseResult ||
                    trigger.CurTargetPicker?.CurTargetPickerType !=
                        TargetPickerType.Arg ||
                    trigger.CurCommandList?.Count != 4)
                    return false;
                hit = trigger;
            }
            if (hit == null || active == null ||
                active.CurCommandList[0] is not CommandPlayAudio activeAudio ||
                hit.CurCommandList[0] is not CommandCauseDamage damage ||
                hit.CurCommandList[1] is not CommandCreateSfx sfx ||
                hit.CurCommandList[2] is not CommandPlayAudio stopAudio ||
                hit.CurCommandList[3] is not CommandPlayAudio startAudio ||
                damage.ParamInt1 == null || damage.ParamInt2 == null ||
                damage.ParamInt3 == null || sfx.SfxID == null ||
                stopAudio.AudioID == null || startAudio.AudioID == null ||
                (damage.CurDamageComputeType != DamageComputeType.NormalDamage &&
                 damage.CurDamageComputeType !=
                     DamageComputeType.FixNumAddAttrPercent) ||
                stopAudio.CreateOrRemove || !startAudio.CreateOrRemove)
                return false;

            rule = new CombatProjectileSimpleHit
            {
                    Enabled = true,
                    DamageMode = (int)damage.CurDamageComputeType,
                    DamageFixedValue = damage.ParamInt1.Value,
                    DamageAttributeType = damage.ParamInt2.Value,
                    DamagePercent = damage.ParamInt3.Value,
                    TriggerDamageRelate = damage.TriggerDamageRelate,
                    SfxId = sfx.SfxID.Value,
                    CreateSfx = sfx.CreateOrRemove,
                    AudioStopId = stopAudio.AudioID.Value,
                    AudioStartId = startAudio.AudioID.Value,
                    ActiveAudioId = activeAudio.AudioID.Value
            };
            return true;
        }
    }
}
