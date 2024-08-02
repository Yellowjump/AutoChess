using System.Collections.Generic;
using Entity.Attribute;
using SkillSystem;
using UnityGameFramework.Runtime;

namespace Entity
{
    public partial class EntityQizi
    {
        public Dictionary<AttributeType,CharacterAttribute> AttributeList = new Dictionary<AttributeType,CharacterAttribute>();

        public CharacterAttribute GetAttribute(AttributeType attributeType)
        {
            if (AttributeList != null&& AttributeList.ContainsKey(attributeType))
            {
                return AttributeList[attributeType];
            }
            Log.Error($"EntityQizi UID:{HeroUID} No Attribute:{attributeType}");
            return null;
        }

        public void TryAddAttribute(CharacterAttribute newAttribute)
        {
            if (newAttribute == null || AttributeList == null ||AttributeList.ContainsKey(newAttribute.CurAttribute))
            {
                Log.Error($"EntityQizi UID:{HeroUID} Add Attribute Error");
                return;
            }
            AttributeList.Add(newAttribute.CurAttribute,newAttribute);
        }
        public void InitAttribute()
        {
            TryAddAttribute(new CharacterAttribute(AttributeType.MaxHp,1000,0,int.MaxValue));
            TryAddAttribute(new CharacterAttribute(AttributeType.Hp,new FixedModifyAttribute(1000,new FixedLimitAttribute<int>(0),new DynamicLimitAttribute<int>(AttributeType.MaxHp,this))));
            TryAddAttribute(new CharacterAttribute(AttributeType.MaxPower,new IntPercentModifyAttribute(100,new FixedLimitAttribute<int>(0),null)));
            TryAddAttribute(new CharacterAttribute(AttributeType.Power,new FixedModifyAttribute(0,new FixedLimitAttribute<int>(0),new DynamicLimitAttribute<int>(AttributeType.MaxPower,this))));
            TryAddAttribute(new CharacterAttribute(AttributeType.AttackDamage,50f));
            TryAddAttribute(new CharacterAttribute(AttributeType.AbilityPower,0f));
            TryAddAttribute(new CharacterAttribute(AttributeType.AttackSpeed,0.8f));
            TryAddAttribute(new CharacterAttribute(AttributeType.Armor,50,isFixModify:false));
            TryAddAttribute(new CharacterAttribute(AttributeType.MagicResist,50,isFixModify:false));
            TryAddAttribute(new CharacterAttribute(AttributeType.ArmorPenetrationNum,0));
            TryAddAttribute(new CharacterAttribute(AttributeType.ArmorPenetrationPercent,0));
            TryAddAttribute(new CharacterAttribute(AttributeType.MagicPenetrationNum,0));
            TryAddAttribute(new CharacterAttribute(AttributeType.MagicPenetrationPercent,0));
        }

        private void UpdateShowSlider()
        {
            var maxHp = (int)GetAttribute(AttributeType.MaxHp).GetFinalValue();
            var curHp = (int)GetAttribute(AttributeType.Hp).GetFinalValue();
            xuetiao.value = curHp / (float)maxHp;
            var maxPower = (int)GetAttribute(AttributeType.MaxPower).GetFinalValue();
            var curPower = (int)GetAttribute(AttributeType.Power).GetFinalValue();
            power.value = curPower / (float)maxPower;
        }

        private void DestoryAttribute()
        {
            AttributeList.Clear();
        }
    }
}