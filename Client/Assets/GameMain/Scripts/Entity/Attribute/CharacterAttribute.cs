using System;
using GameFramework;
using SkillSystem;
using UnityGameFramework.Runtime;

namespace Entity.Attribute
{
    public class CharacterAttribute:IReference
    {
        public AttributeType CurAttributeType;
        private IModifyAttribute _curModify;

        public CharacterAttribute()
        {
            
        }
        public CharacterAttribute Initialize(AttributeType attributeTypeType, IModifyAttribute curModify)
        {
            CurAttributeType = attributeTypeType;
            if (curModify == null)
            {
                Log.Error($"attribute:{attributeTypeType} no Modify");
            }
            _curModify = curModify;
            return this;
        }

        public CharacterAttribute InitializeIntAttr(AttributeType attributeTypeType, int baseValue, int minValue = int.MinValue, int maxValue = int.MaxValue, bool isFixModify = true)
        {
            CurAttributeType = attributeTypeType;
            ILimitedAttribute<int> minLimit = minValue == int.MinValue ? null : ReferencePool.Acquire<FixedLimitAttribute<int>>().Initialize(minValue);
            ILimitedAttribute<int> maxLimit = maxValue == int.MaxValue ? null : ReferencePool.Acquire<FixedLimitAttribute<int>>().Initialize(maxValue);
            if (isFixModify)
            {
                _curModify = ReferencePool.Acquire<FixedModifyAttribute>().Initialize(baseValue, minLimit, maxLimit);
            }
            else
            {
                _curModify = ReferencePool.Acquire<IntPercentModifyAttribute>().Initialize(baseValue, minLimit, maxLimit);
            }

            return this;
        }

        public CharacterAttribute InitializeFloatAttr(AttributeType attributeTypeType, float baseValue, float minValue = float.MinValue, float maxValue = float.MaxValue)
        {
            CurAttributeType = attributeTypeType;
            ILimitedAttribute<float> minLimit = Math.Abs(minValue - float.MinValue) < float.Epsilon ? null : ReferencePool.Acquire<FixedLimitAttribute<float>>().Initialize(minValue);
            ILimitedAttribute<float> maxLimit = Math.Abs(maxValue - float.MaxValue) < float.Epsilon ? null : ReferencePool.Acquire<FixedLimitAttribute<float>>().Initialize(maxValue);
            _curModify = ReferencePool.Acquire<FloatPercentModifyAttribute>().Initialize(baseValue, minLimit, maxLimit);
            return this;
        }
        public object GetFinalValue()
        {
            if (_curModify == null)
            {
                Log.Error($"attribute:{CurAttributeType} no Modify");
                return null;
            }
            return _curModify.GetFinalValue();
        }

        public void SetBaseNum(object baseValue)
        {
            if (_curModify == null)
            {
                Log.Error($"attribute:{CurAttributeType} no Modify");
                return;
            }
            _curModify.SetBaseNum(baseValue);
        }
        public void AddNum(object addValue)
        {
            if (_curModify == null)
            {
                Log.Error($"attribute:{CurAttributeType} no Modify");
                return;
            }
            _curModify.AddNum(addValue);
        }

        public void AddPercent(int addPercentNum)
        {
            if (_curModify == null)
            {
                Log.Error($"attribute:{CurAttributeType} no Modify");
                return;
            }
            _curModify.AddPercent(addPercentNum);
        }

        public void Clear()
        {
            ReferencePool.Release(_curModify);
            _curModify = null;
        }
    }
}