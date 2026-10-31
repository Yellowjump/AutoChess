using System;
using System.Collections.Generic;
using System.IO;
using Entity;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TargetPickerArg:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.Arg;
    }
}
