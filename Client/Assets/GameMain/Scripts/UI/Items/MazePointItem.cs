using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DataTable;
using Object = UnityEngine.Object;
using UnityGameFramework.Runtime;
using GameFramework.Resource;

public class MazePointItem:MonoBehaviour
{
    public Vector2Int Pos = Vector2Int.zero;
    public TextMeshProUGUI Name;
    public GameObject IsPassImg;
    public Image bgImg;
    public Button BtnClick;
    public Action<MazePointItem> OnClickPointCallback;
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
}