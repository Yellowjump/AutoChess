namespace Entity.Attribute
{
    public class FixedLimitAttribute<T>: ILimitedAttribute<T>
    {
        public T LimitValue { get; private set; }
        public FixedLimitAttribute(T limitValue)
        {
            LimitValue = limitValue;
        }
    }
}