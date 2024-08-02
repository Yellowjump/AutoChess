using GameFramework;
using UnityGameFramework.Runtime;

namespace Entity.Attribute
{
    public class IntPercentModifyAttribute:IModifyAttribute
    {
        private int _baseValue;
        private int _additionalValue;
        private int _percentageIncrease;//一既是 千分之1
        private ILimitedAttribute<int> _curMinLimit;
        private ILimitedAttribute<int> _curMaxLimit;

        public IntPercentModifyAttribute()
        {
            
        }

        public IntPercentModifyAttribute Initialize(int baseValue, ILimitedAttribute<int> minLimit, ILimitedAttribute<int> maxLimit)
        {
            _baseValue = baseValue;
            _curMinLimit = minLimit;
            _curMaxLimit = maxLimit;
            return this;
        }
        public IntPercentModifyAttribute(int baseValue, ILimitedAttribute<int> minLimit, ILimitedAttribute<int> maxLimit)
        {
            _baseValue = baseValue;
            _curMinLimit = minLimit;
            _curMaxLimit = maxLimit;
        }
        public object GetFinalValue()
        {
            var finalValue = (int)(_baseValue * (1000 + _percentageIncrease)/1000f) + _additionalValue;
            if (_curMinLimit != null&&finalValue<_curMinLimit.LimitValue)
            {
                finalValue = _curMinLimit.LimitValue;
            }
            if (_curMaxLimit != null&&finalValue>_curMaxLimit.LimitValue)
            {
                finalValue = _curMaxLimit.LimitValue;
            }
            return finalValue;
        }
        public void AddBaseNum(object addValue)
        {
            if (addValue.GetType() != typeof(int))
            {
                Log.Error("addValue should be int");
            }
            var intValue = (int)addValue;
            _baseValue += intValue;
        }

        public void SetBaseNum(object baseNum)
        {
            if (baseNum.GetType() != typeof(int))
            {
                Log.Error("setValue should be int");
            }
            var intValue = (int)baseNum;
            _baseValue = intValue;
        }
        public void AddNum(object addValue)
        {
            if (addValue.GetType() != typeof(int))
            {
                Log.Error("addValue should be Int");
            }
            var intValue = (int)addValue;
            _additionalValue += intValue;
        }

        public void AddPercent(int addPercent)
        {
            _percentageIncrease += addPercent;
        }

        public void Clear()
        {
            _baseValue = 0;
            _additionalValue = 0;
            _percentageIncrease = 0;
            if (_curMinLimit != null)
            {
                ReferencePool.Release(_curMinLimit);
                _curMinLimit = null;
            }

            if (_curMaxLimit != null)
            {
                ReferencePool.Release(_curMaxLimit);
                _curMaxLimit = null;
            }
        }
    }
}