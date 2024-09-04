using SkillSystem;

namespace Entity.Attribute
{
    public class DynamicLimitAttribute<T>:ILimitedAttribute<T>
    {
        private AttributeType _limitMatchAttribute;
        private EntityQizi _owner;
        public T LimitValue
        {
            get
            {
                var iattribute = _owner.GetAttribute(_limitMatchAttribute);
                return (T)iattribute.GetFinalValue();
                
            }
        }

        public DynamicLimitAttribute()
        {
            
        }
        public DynamicLimitAttribute<T> Initialize(AttributeType limitMatchAttribute, EntityQizi owner)
        {
            _limitMatchAttribute = limitMatchAttribute;
            _owner = owner;
            return this;
        }
        public DynamicLimitAttribute(AttributeType limitMatchAttribute, EntityQizi owner)
        {
            _limitMatchAttribute = limitMatchAttribute;
            _owner = owner;
        }

        public void Clear()
        {
            _owner = null;
        }
    }
}