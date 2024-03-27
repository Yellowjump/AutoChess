using liuchengguanli;

namespace SkillSystem
{
    public class TargetPickerBase
    {
        public virtual TargetPickerType CurTargetPickerType => TargetPickerType.NoTarget;
        public EntityBase GetTarget(OneTrigger trigger)
        {
            return null;
        }
    }
}