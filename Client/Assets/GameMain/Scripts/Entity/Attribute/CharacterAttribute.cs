using GameFramework;
using SkillSystem;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace Entity.Attribute
{
    // Compatibility view for skill commands and UI. Values live in CombatAttribute.
    public class CharacterAttribute : IReference
    {
        public AttributeType CurAttributeType;
        private int m_UnitHandle;
        private int m_InitialBase;
        private int m_InitialMin;
        private int m_InitialMax;
        private int m_DynamicMaxType;
        private bool m_PercentBased;

        public CharacterAttribute InitializeIntAttr(AttributeType type, int baseValue,
            int minValue = int.MinValue, int maxValue = int.MaxValue,
            bool isFixModify = true, AttributeType dynamicMaxType = 0)
        {
            CurAttributeType = type;
            m_InitialBase = baseValue;
            m_InitialMin = minValue;
            m_InitialMax = maxValue;
            m_DynamicMaxType = (int)dynamicMaxType;
            m_PercentBased = !isFixModify;
            return this;
        }

        public void Bind(int unitHandle)
        {
            CombatAttributeBridge.AddAttribute(unitHandle, (int)CurAttributeType,
                m_InitialBase, m_InitialMin, m_InitialMax, m_DynamicMaxType, m_PercentBased);
            m_UnitHandle = unitHandle;
            m_InitialBase = 0;
            m_InitialMin = 0;
            m_InitialMax = 0;
            m_DynamicMaxType = 0;
            m_PercentBased = false;
        }

        public object GetFinalValue()
        {
            return CombatAttributeBridge.GetValue(m_UnitHandle, (int)CurAttributeType);
        }

        public void SetBaseNum(object baseValue)
        {
            CombatAttributeBridge.SetBase(m_UnitHandle, (int)CurAttributeType, (int)baseValue);
        }

        public void AddNum(object addValue)
        {
            CombatAttributeBridge.AddNum(m_UnitHandle, (int)CurAttributeType, (int)addValue);
        }

        public void AddPercent(int addPercentNum)
        {
            if (!CombatAttributeBridge.AddPercent(m_UnitHandle, (int)CurAttributeType, addPercentNum))
            {
                Log.Error("fixedModifyAttribute cant AddPercent");
            }
        }

        public void Clear()
        {
            CurAttributeType = 0;
            m_UnitHandle = 0;
            m_InitialBase = 0;
            m_InitialMin = 0;
            m_InitialMax = 0;
            m_DynamicMaxType = 0;
            m_PercentBased = false;
        }
    }
}
