using System.Collections.Generic;
using Entity;
using UnityEngine.Pool;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    public class TargetPickerSkillCasterCurTarget:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.SkillCasterCurTarget;
    }
}
