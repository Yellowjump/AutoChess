using System;
using System.IO;
using Entity;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TargetPickerArg:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.Arg;
        public override EntityBase GetTarget(OneTrigger trigger,object arg = null)
        {
            if (arg is EntityQizi qizi)
            {
                return qizi;
            }
            return null;
        }
    }
}