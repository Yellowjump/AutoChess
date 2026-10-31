using System.Collections.Generic;
using Entity;
using UnityEngine.Pool;

namespace SkillSystem
{
    public class TargetPickerTriggerOwner:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.TriggerOwner;
    }
}
