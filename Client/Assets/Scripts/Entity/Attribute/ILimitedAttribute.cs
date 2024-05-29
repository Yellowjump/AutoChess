namespace Entity.Attribute
{
    public interface ILimitedAttribute<T>
    {
        T LimitValue { get; }
    }
}