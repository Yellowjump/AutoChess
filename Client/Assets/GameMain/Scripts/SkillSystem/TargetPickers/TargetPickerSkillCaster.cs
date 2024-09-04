using Entity;

namespace SkillSystem
{
    public class TargetPickerSkillCaster:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.SkillCaster;
        public override EntityBase GetTarget(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.ParentTriggerList != null && trigger.ParentTriggerList.ParentSkill != null && trigger.ParentTriggerList.ParentSkill.Caster != null)
            {
                return trigger.ParentTriggerList.ParentSkill.Caster;
            }
            return null;
        }
    }
}