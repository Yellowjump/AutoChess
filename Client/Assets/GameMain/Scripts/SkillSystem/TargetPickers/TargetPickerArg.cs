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
        public override List<EntityBase> GetTarget(OneTrigger trigger,object arg = null)
        {
            if (arg is EntityQizi qizi)
            {
                List<EntityBase> targetList = ListPool<EntityBase>.Get();
                targetList.Add(qizi);
                return targetList;
            }
            return null;
        }
    }
}