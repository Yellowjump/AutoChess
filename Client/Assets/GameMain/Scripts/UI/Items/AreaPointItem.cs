using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DataTable;
using Object = UnityEngine.Object;
using UnityGameFramework.Runtime;
using GameFramework.Resource;

public class AreaPointItem:MonoBehaviour
{
    public int Index;
    public TextMeshProUGUI Name;
    public GameObject IsPassImg;
    public Image bgImg;
    public Button BtnClick;
    public Action<AreaPointItem> OnClickPointCallback;
    private LoadAssetCallbacks _loadIconCallback;

    public void Init()
    {
        _loadIconCallback = new LoadAssetCallbacks(LoadBgImgCallbacks);
    }
    private void Start()
    {
        BtnClick.onClick.AddListener(OnClickBtn);
    }

    private void OnClickBtn()
    {
        OnClickPointCallback.Invoke(this);
    }

    public void GetBgImg(int IconID)
    {
        var assetsTable = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
        if (!assetsTable.HasDataRow(IconID))
        {
            Log.Error($"assetsTable Table not Contain {IconID}");
            return;
        }
        var assetData = assetsTable[IconID];
        GameEntry.Resource.LoadAsset(assetData.AssetPath,typeof(Sprite),_loadIconCallback);
    }

    private void LoadBgImgCallbacks(string assetName, object asset, float duration, object userData)
    {
        Sprite sp = asset as Sprite;
        if (sp != null)
        {
            bgImg.sprite = sp;
        }
    }

    public void SetOpaque(float opaque)
    {
        Name.color = new Color(Name.color.r,Name.color.g,Name.color.b,opaque);
        bgImg.color =  new Color(bgImg.color.r,bgImg.color.g,bgImg.color.b,opaque);
    }
}