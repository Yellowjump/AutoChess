using System.Collections.Generic;
using Entity;
using UnityEngine.Pool;

namespace SkillSystem
{
    public class TargetPickerSkillCasterCurTarget:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.SkillCasterCurTarget;
        public override void GetTarget(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.ParentTriggerList != null && trigger.ParentTriggerList.ParentSkill != null && trigger.ParentTriggerList.ParentSkill.Caster != null)
            {
                trigger.CurTargetList.Add(trigger.ParentTriggerList.ParentSkill.Caster.CurAttackTarget);
            }
        }
    }
}