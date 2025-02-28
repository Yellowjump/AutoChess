using System.Collections.Generic;
using DataTable;
using Entity.Attribute;
using GameFramework;
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
            if (newAttribute == null || AttributeList == null ||AttributeList.ContainsKey(newAttribute.CurAttributeType))
            {
                Log.Error($"EntityQizi UID:{HeroUID} Add Attribute Error");
                return;
            }
            AttributeList.Add(newAttribute.CurAttributeType,newAttribute);
        }
        public void InitAttribute()
        {
            var heroTable = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (!heroTable.HasDataRow(HeroID))
            {
                Log.Error($"heroID:{HeroID} invalid no match TableRow");
                return;
            }

            var attributeID = heroTable[HeroID].AttributeID;
            var heroAttributeTable = GameEntry.DataTable.GetDataTable<DRHeroAttribute>("HeroAttribute");
            if (!heroAttributeTable.HasDataRow(attributeID))
            {
                Log.Error($"attributeID:{attributeID} invalid no match TableRow");
                return;
            }
            var attributeData = heroAttributeTable[attributeID];
            TryAddAttribute(ReferencePool.Acquire<CharacterAttribute>().InitializeIntAttr(AttributeType.MaxHp,attributeData.Hp,0,int.MaxValue));
            TryAddAttribute(ReferencePool.Acquire<CharacterAttribute>().Initialize(AttributeType.Hp,ReferencePool.Acquire<FixedModifyAttribute>().Initialize(attributeData.Hp,ReferencePool.Acquire<FixedLimitAttribute<int>>().Initialize(0),ReferencePool.Acquire<DynamicLimitAttribute<int>>().Initialize(AttributeType.MaxHp,this))));
            TryAddAttribute(ReferencePool.Acquire<CharacterAttribute>().InitializeIntAttr(AttributeType.MaxPower,attributeData.Power,0,int.MaxValue));
            TryAddAttribute(ReferencePool.Acquire<CharacterAttribute>().Initialize(AttributeType.Power,ReferencePool.Acquire<FixedModifyAttribute>().Initialize(0,ReferencePool.Acquire<FixedLimitAttribute<int>>().Initialize(0),ReferencePool.Acquire<DynamicLimitAttribute<int>>().Initialize(AttributeType.MaxPower,this))));
            TryAddAttribute(ReferencePool.Acquire<CharacterAttribute>().InitializeIntAttr(AttributeType.HuDun,0,0));
            TryAddAttribute(ReferencePool.Acquire<CharacterAttribute>().InitializeIntAttr(AttributeType.AttackDamage,attributeData.AttackDamage,isFixModify:false));
            TryAddAttribute(ReferencePool.Acquire<CharacterAttribute>().InitializeIntAttr(AttributeType.AttackSpeed,attributeData.AttackSpeed,0));
            TryAddAttribute(ReferencePool.Acquire<CharacterAttribute>().InitializeIntAttr(AttributeType.HuDunBoost,attributeData.HudunBoost,0,int.MaxValue));//todo
            TryAddAttribute(ReferencePool.Acquire<CharacterAttribute>().InitializeIntAttr(AttributeType.CooldownReduce,attributeData.CooldownReduce,0,int.MaxValue));
        }

        public void TryAddAttrValue(AttributeType attrType,int value)
        {
            var attr = GetAttribute(attrType);
            attr.AddNum(value);
        }
        private void UpdateShowSlider()
        {
            if (xuetiao != null)
            {
                var maxHp = (int)GetAttribute(AttributeType.MaxHp).GetFinalValue();
                var curHp = (int)GetAttribute(AttributeType.Hp).GetFinalValue();
                var curHuDun=(int)GetAttribute(AttributeType.HuDun).GetFinalValue();
                if (curHp + curHuDun > maxHp)
                {
                    float curSum = curHp + curHuDun;
                    xuetiao.value = curHp / curSum;
                    hudun.value = curHuDun / curSum;
                }
                else
                {
                    xuetiao.value = curHp / (float)maxHp;
                    hudun.value = curHuDun / (float)maxHp;
                }
            }

            if (power != null)
            {
                var maxPower = (int)GetAttribute(AttributeType.MaxPower).GetFinalValue();
                var curPower = (int)GetAttribute(AttributeType.Power).GetFinalValue();
                power.value = curPower / (float)maxPower;
            }
        }

        private void DestoryAttribute()
        {
            if (AttributeList != null)
            {
                foreach (var oneAttrKeyValue in AttributeList)
                {
                    ReferencePool.Release(oneAttrKeyValue.Value);
                }
            }
            AttributeList.Clear();
        }
    }
}