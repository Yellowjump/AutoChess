using System.Collections.Generic;
using Entity;
using UnityEngine.Pool;

namespace SkillSystem
{
    public class TargetPickerSkillCaster:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.SkillCaster;
    }
}
