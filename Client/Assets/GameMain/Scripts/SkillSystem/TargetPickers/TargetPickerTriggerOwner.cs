using System.Collections.Generic;
using Entity;
using UnityEngine.Pool;

namespace SkillSystem
{
    public class TargetPickerTriggerOwner:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.TriggerOwner;
        public override List<EntityBase> GetTarget(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.ParentTriggerList != null)
            {
                List<EntityBase> targetList = ListPool<EntityBase>.Get();
                targetList.Add(trigger.ParentTriggerList.Owner);
                return targetList;
            }
            return null;
        }
    }
}