using DataTable;
using SkillSystem;
using TMPro;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameMain.Scripts.UI.Items
{
    public class ItemTip:MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _itemTipName;
        [SerializeField] private TextMeshProUGUI _itemTipRarity;
        [SerializeField] private TextMeshProUGUI _itemTipCast;
        [SerializeField] private TextMeshProUGUI _itemTipType;
        [SerializeField] private TextMeshProUGUI _itemTipCDNum;
        [SerializeField] private TextMeshProUGUI _itemTipDec;
        [SerializeField] private TextMeshProUGUI _itemTipAtkDistance;
        public int ItemID;

        public void FreshTip()
        {
            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            if (!itemTable.HasDataRow(ItemID))
            {
                Log.Error($"Item Table not Contain {ItemID}");
                return;
            }

            _itemTipName.text = GameEntry.Localization.GetString(itemTable[ItemID].Name);
            _itemTipDec.text = itemTable[ItemID].Decs;
            _itemTipRarity.color = ConstValue.RarityColorList[itemTable[ItemID].Rarity];
            _itemTipRarity.text = ConstValue.RarityNameList[itemTable[ItemID].Rarity];
            var skillTable = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
            if (skillTable.HasDataRow(itemTable[ItemID].SkillID))
            {
                var skillData = skillTable[itemTable[ItemID].SkillID];
                if (skillData.SkillType == (int)SkillType.NormalSkill)
                {
                    _itemTipAtkDistance.text = skillData.SkillRange.ToString();
                }
                _itemTipAtkDistance.gameObject.SetActive(skillData.SkillType == (int)SkillType.NormalSkill);
                _itemTipCast.gameObject.SetActive(skillData.SkillType != (int)SkillType.PassiveSkill);
                string castOrGet = string.Empty;
                if (skillData.CastPower == 0)
                {
                    castOrGet = "无消耗";
                }
                else if (skillData.CastPower > 0)
                {
                    castOrGet = $"消耗{skillData.CastPower}法力值";
                }
                else
                {
                    castOrGet = $"获得{-skillData.CastPower}法力值";
                }
                _itemTipCast.text = castOrGet;
            
            }
        }
    }
}