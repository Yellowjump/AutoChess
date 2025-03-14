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
        public int CurHeroUID = 0;
        public void FreshTip()
        {
            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            if (!itemTable.HasDataRow(ItemID))
            {
                Log.Error($"Item Table not Contain {ItemID}");
                return;
            }

            _itemTipName.text = GameEntry.Localization.GetString(itemTable[ItemID].Name);
            if (!string.IsNullOrEmpty(itemTable[ItemID].Decs))
            {
                _itemTipDec.text = GameEntry.HeroManager.FormatItemDesc(GameEntry.Localization.GetString(itemTable[ItemID].Decs),CurHeroUID);
            }
            else
            {
                _itemTipDec.text = string.Empty;
            }
            _itemTipRarity.color = ConstValue.RarityColorList[itemTable[ItemID].Rarity];
            _itemTipRarity.text = GameEntry.Localization.GetString(ConstValue.RarityNameList[itemTable[ItemID].Rarity]);
            var skillTable = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
            _itemTipType.gameObject.SetActive(itemTable[ItemID].SkillID!=0);
            _itemTipAtkDistance.gameObject.SetActive(itemTable[ItemID].SkillID!=0);
            _itemTipCDNum.gameObject.SetActive(itemTable[ItemID].SkillID!=0);
            _itemTipCast.gameObject.SetActive(itemTable[ItemID].SkillID!=0);
            if (skillTable.HasDataRow(itemTable[ItemID].SkillID))
            {
                var skillData = skillTable[itemTable[ItemID].SkillID];
                if (skillData.SkillType == (int)SkillType.NormalSkill)
                {
                    _itemTipAtkDistance.text = Mathf.FloorToInt(skillData.SkillRange/1000f).ToString();
                }
                _itemTipAtkDistance.gameObject.SetActive(skillData.SkillRange != 0);
                //_itemTipCast.gameObject.SetActive(skillData.SkillType != (int)SkillType.PassiveSkill);
                string castOrGet = string.Empty;
                if (skillData.CastPower == 0)
                {
                    castOrGet = GameEntry.Localization.GetString(EnumLanguage.NoCastPower) ;
                }
                else if (skillData.CastPower > 0)
                {
                    castOrGet = string.Format(GameEntry.Localization.GetString(EnumLanguage.CastPowerNum),skillData.CastPower);
                }
                else
                {
                    castOrGet = string.Format(GameEntry.Localization.GetString(EnumLanguage.GetPowerNum),-skillData.CastPower);
                }
                _itemTipCast.text = castOrGet;
                var key = EnumLanguage.SkillType_Active;
                switch (skillData.SkillType)
                {
                    case (int)SkillType.NoAnimSkill:
                        key = EnumLanguage.SkillType_Auto;
                        break;
                    case (int)SkillType.PassiveSkill:
                        key = EnumLanguage.SkillType_Passive;
                        break;
                }
                _itemTipType.text = GameEntry.Localization.GetString(key);
                _itemTipCDNum.text = Mathf.FloorToInt(skillData.CDMs / 1000f).ToString();
            }
        }
    }
}