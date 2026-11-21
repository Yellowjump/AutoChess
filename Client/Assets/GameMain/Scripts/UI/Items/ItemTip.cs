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
        [SerializeField] private TextMeshProUGUI _itemTipType;
        [SerializeField] private TextMeshProUGUI _itemTipCDNum;
        [SerializeField] private TextMeshProUGUI _itemTipDec;
        [SerializeField] private TextMeshProUGUI _itemTipAtkDistance;
        public int ItemID;
        public int CurHeroUID = 0;
        internal void ShowCarried(WorldTimelineDebug.WorldTimelineDebugView.EquipmentEntry entry, Skill skill)
        {
            _itemTipName.text = entry.Name;
            _itemTipDec.text = entry.Description?.Replace("#A9A08A", "#665940");
            _itemTipRarity.text = string.Empty;
            _itemTipType.text = entry.SkillTypeName;
            _itemTipType.gameObject.SetActive(!string.IsNullOrEmpty(entry.SkillTypeName));
            bool casts = skill != null && skill.CurSkillType != SkillType.PassiveSkill;
            LayoutDetails(casts ? skill.CurCastCDMs : 0, casts ? skill.SkillRange : 0);
        }
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
                _itemTipDec.text = HotGameEntry.HeroManager.FormatItemDesc(GameEntry.Localization.GetString(itemTable[ItemID].Decs),CurHeroUID);
            }
            else
            {
                _itemTipDec.text = string.Empty;
            }
            _itemTipRarity.color = Color.Lerp(ConstValue.RarityColorList[itemTable[ItemID].Rarity], new Color(.18f,.14f,.09f), .65f);
            _itemTipRarity.text = GameEntry.Localization.GetString(ConstValue.RarityNameList[itemTable[ItemID].Rarity]);
            var item = itemTable[ItemID];
            var skillData = ItemSkills.Highest(item);
            _itemTipType.gameObject.SetActive(skillData != null || item.AttrAdd?.Count > 0);
            _itemTipType.text = ItemSkills.Label(skillData == null ? SkillType.PassiveSkill : (SkillType)skillData.SkillType);
            bool casts = skillData != null && skillData.SkillType != (int)SkillType.PassiveSkill;
            LayoutDetails(casts ? skillData.CDMs : 0, casts ? skillData.SkillRange : 0);
        }

        private void LayoutDetails(int cooldownMs, int rangeMm)
        {
            bool english = GameEntry.Localization.Language == GameFramework.Localization.Language.English;
            _itemTipCDNum.gameObject.SetActive(cooldownMs > 0);
            _itemTipAtkDistance.gameObject.SetActive(rangeMm > 0);
            if (cooldownMs > 0)
                _itemTipCDNum.text = (english ? "CD " : "冷却 ") + (cooldownMs / 1000f).ToString("0.##") + (english ? "s" : "秒");
            if (rangeMm > 0)
                _itemTipAtkDistance.text = (english ? "Range " : "射程 ") + (rangeMm / 1000f).ToString("0.##");

            var body = _itemTipDec.rectTransform;
            var paper = (RectTransform)body.parent;
            float textHeight = _itemTipDec.GetPreferredValues(_itemTipDec.text, body.rect.width, Mathf.Infinity).y;
            float bodyHeight = Mathf.Clamp(Mathf.Ceil(textHeight) + 6f, 38f, 440f);
            body.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyHeight);
            paper.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, -body.anchoredPosition.y + bodyHeight + 22f);
        }
    }
}
