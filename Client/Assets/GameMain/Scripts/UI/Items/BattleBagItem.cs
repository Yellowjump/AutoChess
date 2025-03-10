﻿using System;
using DataTable;
using GameFramework.Resource;
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

    public int ItemID;
    public int itemNum = 0;
    public Action<BattleBagItem> OnClickPointCallback;
    public Action<BattleBagItem> OnPointEnterCallback;
    public Action<BattleBagItem> OnPointExitCallback;
    private LoadAssetCallbacks _loadIconCallback;
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
        ItemNameTmp.text = itemTable[ItemID].Name;
        FreshNum();
        var itemData = itemTable[ItemID];
        var assetsTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
        if (!assetsTable.HasDataRow(itemData.IconID))
        {
            Log.Error($"assetsTable Table not Contain {itemData.IconID}");
            return;
        }

        var assetData = assetsTable[itemData.IconID];
        GameEntry.Resource.LoadAsset(assetData.AssetPath,typeof(Sprite),_loadIconCallback);
    }

    public void OnRelease()
    {
        if (Icon.sprite != null)
        {
            GameEntry.Resource.UnloadAsset(Icon.sprite);
        }
    }
    public void FreshNum()
    {
        ItemNumTmp.gameObject.SetActive(CurItemType is ItemType.Bag or ItemType.InJoinCraft&&itemNum>1);
        ItemNumTmp.text = itemNum.ToString();
    }
    private void OnIconLoadSuccessCallback(string assetName, object asset, float duration, object userData)
    {
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