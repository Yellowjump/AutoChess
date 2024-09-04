using GameFramework;

namespace Entity.Attribute
{
    public class FixedLimitAttribute<T>: ILimitedAttribute<T>
    {
        public T LimitValue { get; private set; }
        public FixedLimitAttribute(T limitValue)
        {
            LimitValue = limitValue;
        }
        public FixedLimitAttribute()
        {
            LimitValue = default(T);
        }
        // 初始化方法
        public FixedLimitAttribute<T> Initialize(T limitValue)
        {
            LimitValue = limitValue;
            return this;
        }
        public void Clear()
        {
            
        }
    }
}