using System.Collections.Generic;
using Entity;
using UnityEngine.Pool;

namespace SkillSystem
{
    public class TargetPickerSkillCaster:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.SkillCaster;
        public override List<EntityBase> GetTarget(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.ParentTriggerList != null && trigger.ParentTriggerList.ParentSkill != null && trigger.ParentTriggerList.ParentSkill.Caster != null)
            {
                List<EntityBase> targetList = ListPool<EntityBase>.Get();
                targetList.Add(trigger.ParentTriggerList.ParentSkill.Caster);
                return targetList;
            }
            return null;
        }
    }
}