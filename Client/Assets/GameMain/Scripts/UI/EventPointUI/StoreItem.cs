using System;
using DataTable;
using GameFramework.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class StoreItem:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    public Image Icon;
    public Image Rarity;
    public Button BtnClick;
    public TextMeshProUGUI ItemStoreCast;

    public int ItemID;
    public Action<StoreItem> OnClickPointCallback;
    public Action<StoreItem> OnPointEnterCallback;
    public Action<StoreItem> OnPointExitCallback;
    private LoadAssetCallbacks _loadIconCallback;
    public void Init()
    {
        BtnClick.onClick.AddListener(OnClickBtn);
        _loadIconCallback = new LoadAssetCallbacks(OnIconLoadSuccessCallback);
    }

    public void Fresh()
    {
        BtnClick.interactable = true;
        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        if (!itemTable.HasDataRow(ItemID))
        {
            Log.Error($"Item Table not Contain {ItemID}");
            return;
        }

        ItemStoreCast.text = itemTable[ItemID].StoreCoin.ToString();
        //Rarity.color = ConstValue.RarityColorList[itemTable[ItemID].Rarity];
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

    public void SetBtnInteractFalse()
    {
        BtnClick.interactable = false;
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