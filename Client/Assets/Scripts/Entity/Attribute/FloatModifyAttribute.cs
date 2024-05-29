using UnityGameFramework.Runtime;

namespace Entity.Attribute
{
    public class FloatPercentModifyAttribute:IModifyAttribute
    {
        private float _baseValue;
        private float _additionalValue;
        private int _percentageIncrease;//一既是 千分之1
        private ILimitedAttribute<float> _curMinLimit;
        private ILimitedAttribute<float> _curMaxLimit;

        public FloatPercentModifyAttribute(float baseValue, ILimitedAttribute<float> minLimit, ILimitedAttribute<float> maxLimit)
        {
            _baseValue = baseValue;
            _curMinLimit = minLimit;
            _curMaxLimit = maxLimit;
        }
        public object GetFinalValue()
        {
            var finalValue = (_baseValue * (1000 + _percentageIncrease)/1000f) + _additionalValue;
            if (_curMinLimit != null&&finalValue<_curMinLimit.LimitValue)
            {
                finalValue = _curMinLimit.LimitValue;
            }
            if (_curMinLimit != null&&finalValue>_curMaxLimit.LimitValue)
            {
                finalValue = _curMaxLimit.LimitValue;
            }
            return finalValue;
        }

        public void AddBaseNum(object addValue)
        {
            if (addValue.GetType() != typeof(float))
            {
                Log.Error("addValue should be float");
            }
            var intValue = (float)addValue;
            _baseValue += intValue;
        }

        public void SetBaseNum(object baseNum)
        {
            if (baseNum.GetType() != typeof(float))
            {
                Log.Error("setValue should be float");
            }
            var intValue = (float)baseNum;
            _baseValue = intValue;
        }

        public void AddNum(object addValue)
        {
            if (addValue.GetType() != typeof(float))
            {
                Log.Error("addValue should be float");
            }
            var intValue = (float)addValue;
            _additionalValue += intValue;
        }

        public void AddPercent(int addPercent)
        {
            _percentageIncrease += addPercent;
        }
    }
}