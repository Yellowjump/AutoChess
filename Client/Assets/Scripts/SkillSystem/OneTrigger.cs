using System.Collections.Generic;
using liuchengguanli;

namespace SkillSystem
{
    public class OneTrigger
    {
        public TriggerType CurTriggerType;
        public ConditionBase CurCondition;
        public TargetPickerBase CurTargetPicker;
        public List<CommandBase> CurCommandList = new List<CommandBase>();
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

                if (CurCommandList != null && CurCommandList.Count != 0)
                {
                    foreach (var oneCommand in CurCommandList)
                    {
                        oneCommand?.OnExecute(this);
                    }
                }
                
            }
        }
    }
}