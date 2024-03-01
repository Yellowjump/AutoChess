using liuchengguanli;

namespace SkillSystem
{
    public class OneTrigger
    {
        public TriggerType CurTriggerType;
        public ConditionBase CurCondition;
        public TargetPickerBase CurTargetPicker;
        public CommandBase CurCommand;
        public EntityBase CurTarget;
        public void OnActive()
        {
            if (CurTriggerType == TriggerType.OnActive)
            {
                OnTrigger();
            }
        }
        public void OnTrigger()
        {
            if (CurCondition != null && CurCondition.OnCheck(this)==!CurCondition.ReverseResult)
            {
                if (CurTargetPicker != null)
                {
                    CurTarget = CurTargetPicker.GetTarget(this);
                }
                CurCommand?.OnExecute(this);
            }
        }
    }
}