using System;
using SkillSystem;
using UnityGameFramework.Runtime;

namespace Entity.Attribute
{
    public class CharacterAttribute
    {
        public AttributeType CurAttribute;
        private IModifyAttribute _curModify;
        public AttributeType AttributeType => CurAttribute;
        public object GetAttribute()
        {
            return this;
        }
        public CharacterAttribute(AttributeType attributeType, IModifyAttribute curModify)
        {
            CurAttribute = attributeType;
            if (curModify == null)
            {
                Log.Error($"attribute:{attributeType} no Modify");
            }
            _curModify = curModify;
        }

        public CharacterAttribute(AttributeType attributeType, int baseValue, int minValue = int.MinValue, int maxValue = int.MaxValue, bool isFixModify = true)
        {
            CurAttribute = attributeType;
            ILimitedAttribute<int> minLimit = minValue == int.MinValue ? null : new FixedLimitAttribute<int>(minValue);
            ILimitedAttribute<int> maxLimit = maxValue == int.MaxValue ? null : new FixedLimitAttribute<int>(maxValue);
            if (isFixModify)
            {
                _curModify = new FixedModifyAttribute(baseValue, minLimit, maxLimit);
            }
            else
            {
                _curModify = new IntPercentModifyAttribute(baseValue, minLimit, maxLimit);
            }
        }

        public CharacterAttribute(AttributeType attributeType, float baseValue, float minValue = float.MinValue, float maxValue = float.MaxValue)
        {
            CurAttribute = attributeType;
            ILimitedAttribute<float> minLimit = Math.Abs(minValue - float.MaxValue) < float.Epsilon ? null : new FixedLimitAttribute<float>(minValue);
            ILimitedAttribute<float> maxLimit = Math.Abs(maxValue - float.MaxValue) < float.Epsilon ? null : new FixedLimitAttribute<float>(maxValue);
            _curModify = new FloatPercentModifyAttribute(baseValue, minLimit, maxLimit);
        }
        public object GetFinalValue()
        {
            if (_curModify == null)
            {
                Log.Error($"attribute:{CurAttribute} no Modify");
                return null;
            }
            return _curModify.GetFinalValue();
        }

        public void AddNum(object addValue)
        {
            if (_curModify == null)
            {
                Log.Error($"attribute:{CurAttribute} no Modify");
                return;
            }
            _curModify.AddNum(addValue);
        }

        public void AddPercent(int addPercentNum)
        {
            if (_curModify == null)
            {
                Log.Error($"attribute:{CurAttribute} no Modify");
                return;
            }
            _curModify.AddPercent(addPercentNum);
        }
    }
}