using GameFramework;

namespace Entity.Attribute
{
    public interface ILimitedAttribute<T>:IReference
    {
        T LimitValue { get; }
    }
}