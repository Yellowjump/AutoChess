using Entity;

namespace SkillSystem
{
    public class TargetPickerTriggerOwner:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.TriggerOwner;
        public override EntityBase GetTarget(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.ParentTriggerList != null)
            {
                return trigger.ParentTriggerList.Owner;
            }
            return null;
        }
    }
}