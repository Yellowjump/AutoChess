using System;
using DataTable;
using Entity;
using GameFramework.Resource;
using SkillSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class BattleBagItem:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    public enum ItemType
    {
        /// <summary>
        /// 在背包中，点击参与合成
        /// </summary>
        Bag,
        /// <summary>
        /// 在合成中，点击退出合成列表
        /// </summary>
        InJoinCraft,
        /// <summary>
        /// 合成结果
        /// </summary>
        CraftResult,
        /// <summary>
        /// 英雄装备
        /// </summary>
        HeroEquip,
        /// <summary>
        /// 战斗详情中的英雄装备
        /// </summary>
        BattleDetailHeroEquip,
        /// <summary>
        /// 奖励
        /// </summary>
        RewardItem,
    }

    public ItemType CurItemType = ItemType.Bag;
    public Image Icon;
    public Image Rarity;
    public Button BtnClick;
    public TextMeshProUGUI ItemNumTmp;
    public TextMeshProUGUI ItemNameTmp;
    public Image CDFillImage;
    public int ItemID;
    public int ItemUniqueID;
    public int itemNum = 0;
    public Action<BattleBagItem> OnClickPointCallback;
    public Action<BattleBagItem> OnPointEnterCallback;
    public Action<BattleBagItem> OnPointExitCallback;
    private LoadAssetCallbacks _loadIconCallback;
    internal WorldTimelineDebug.WorldTimelineDebugView.EquipmentEntry CarriedEntry { get; private set; }
    internal Skill CarriedSkill { get; private set; }
    private int iconRequest;

    internal void ShowCarried(WorldTimelineDebug.WorldTimelineDebugView.EquipmentEntry entry, Sprite icon, Skill skill)
    {
        CarriedEntry = entry;
        CarriedSkill = skill;
        Icon.sprite = icon;
        ItemNameTmp.text = entry.Name;
        ItemNumTmp.text = "×" + entry.Count;
        ItemNumTmp.gameObject.SetActive(entry.Count > 1);
        gameObject.SetActive(true);
    }

    internal void RefreshCarriedCooldown()
    {
        if (CDFillImage == null) return;
        bool active = CarriedSkill != null && CarriedSkill.CurSkillType != SkillType.PassiveSkill && CarriedSkill.CurCastCDMs > 0;
        CDFillImage.gameObject.SetActive(active);
        CDFillImage.fillAmount = active ? CarriedSkill.LeftSkillCD * 1000 / CarriedSkill.CurCastCDMs : 0;
    }
    public void Init()
    {
        BtnClick.onClick.AddListener(OnClickBtn);
        _loadIconCallback = new LoadAssetCallbacks(OnIconLoadSuccessCallback);
    }

    public void Fresh()
    {
        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        if (!itemTable.HasDataRow(ItemID))
        {
            Log.Error($"Item Table not Contain {ItemID}");
            return;
        }
        gameObject.SetActive(false);
        string type = ItemSkills.ItemLabel(itemTable[ItemID]);
        string name = GameEntry.Localization.GetString(itemTable[ItemID].Name);
        ItemNameTmp.text = string.IsNullOrEmpty(type) ? name : type + " · " + name;
        FreshNum();
        var itemData = itemTable[ItemID];
        if (ItemID is SummonedArt.Cloud or SummonedArt.Mirror)
        {
            iconRequest++;
            Icon.sprite = Resources.Load<Sprite>(ItemID == SummonedArt.Cloud ? "StormCloud" : "SunMirror");
            gameObject.SetActive(true);
            return;
        }
        var assetsTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
        if (!assetsTable.HasDataRow(itemData.IconID))
        {
            Log.Error($"assetsTable Table not Contain {itemData.IconID}");
            return;
        }

        var assetData = assetsTable[itemData.IconID];
        GameEntry.Resource.LoadAsset(assetData.AssetPath,typeof(Sprite),_loadIconCallback, ++iconRequest);
    }

    public void OnRelease()
    {
        iconRequest++;
        CarriedEntry = null;
        CarriedSkill = null;
    }
    public void FreshNum()
    {
        ItemNumTmp.gameObject.SetActive(CurItemType is ItemType.Bag or ItemType.InJoinCraft&&itemNum>1);
        ItemNumTmp.text = itemNum.ToString();
    }

    public void FreshCd(EntityQizi entity)
    {
        if (CDFillImage != null)
        {
            CDFillImage.gameObject.SetActive(CurItemType == ItemType.BattleDetailHeroEquip);
            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            if (!itemTable.HasDataRow(ItemID))
            {
                Log.Error($"Item Table not Contain {ItemID}");
                return;
            }

            if (entity != null)
            {
                var skill = entity.GetSkillByItemUniqID(ItemUniqueID);
                if (skill == null||skill.CurSkillType == SkillType.PassiveSkill)
                {
                    CDFillImage.fillAmount = 0;
                    return;
                }
                var leftCd = skill.LeftSkillCD;
                var cdMs = skill.CurCastCDMs;
                if (cdMs <= 0)
                {
                    CDFillImage.fillAmount = 0;
                    return;
                }
                CDFillImage.fillAmount = leftCd*1000/cdMs;
            }
        }
    }
    private void OnIconLoadSuccessCallback(string assetName, object asset, float duration, object userData)
    {
        if (!(userData is int request) || request != iconRequest) return;
        Sprite sp = asset as Sprite;
        if (sp != null)
        {
            Icon.sprite = sp;
        }
        gameObject.SetActive(true);
    }
    private void OnClickBtn()
    {
        OnClickPointCallback?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnPointEnterCallback?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnPointExitCallback?.Invoke(this);
    }
}