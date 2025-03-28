using System.Collections.Generic;
using Entity;
using UnityEngine.Pool;

namespace SkillSystem
{
    public class TargetPickerTriggerOwner:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.TriggerOwner;
        public override void GetTarget(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.ParentTriggerList != null)
            {
                trigger.CurTargetList.Add(trigger.ParentTriggerList.Owner);
            }
        }
    }
}