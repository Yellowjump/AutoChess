using UnityGameFramework.Runtime;

namespace Entity.Attribute
{
    public class FixedModifyAttribute:IModifyAttribute
    {
        private int _baseValue;
        private ILimitedAttribute<int> _curMinLimit;
        private ILimitedAttribute<int> _curMaxLimit;

        public FixedModifyAttribute(int baseValue, ILimitedAttribute<int> minLimit, ILimitedAttribute<int> maxLimit)
        {
            _baseValue = baseValue;
            _curMinLimit = minLimit;
            _curMaxLimit = maxLimit;
        }
        public object GetFinalValue()
        {
            var finalValue = _baseValue;
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
                Log.Error("addValue should be int");
            }
            var intValue = (int)baseNum;
            _baseValue = intValue;
        }

        public void AddNum(object addValue)
        {
            if (addValue.GetType() != typeof(int))
            {
                Log.Error("addValue should be int");
            }
            var intValue = (int)addValue;
            _baseValue += intValue;
        }

        public void AddPercent(int addPercent)
        {
            Log.Error("fixedModifyAttribute cant AddPercent");
        }
    }
}