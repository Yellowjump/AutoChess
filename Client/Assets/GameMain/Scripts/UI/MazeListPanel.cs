using System;
using System.Collections;
using System.Collections.Generic;
using GameFramework.Event;
using Maze;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using SelfEventArg;

public class MazeListPanelCtrl : UIFormLogic
{
    [SerializeField] private GameObject _pointTemp;
    [SerializeField] private GameObject _lineTemp;
    [SerializeField] private GameObject _invisbleParent;
    [SerializeField] private GameObject _showPointParent;
    [SerializeField] private GameObject _showLineParent;
    [SerializeField] private Vector2 ItemStartPos = new Vector2(-500, -500);
    [SerializeField] private Vector2 ItemOffSet = new Vector2(150, 150);
    private ObjectPool<GameObject> _pointPool;
    private ObjectPool<GameObject> _linePool;
    [SerializeField] private Button _btnClose;
    //-----------fogStart-------------------
    [SerializeField] private RawImage _fogImage;
    [SerializeField] private int mapWidth=50;
    [SerializeField] private int mapHeight=50;
    [SerializeField] private int onePointFarRadius = 20;
    //public Color32[] colorBuffer;//r装当前是否可见的透明度
    [SerializeField] private Material _blurMat;//模糊的材质
    private RenderBuffer FogTargetBuffer;//放入image中的renderBuffer;
    private Texture2D _maskTexture;//生成的透明度mask
    //-----------fogEnd
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnClose.onClick.AddListener(() => { GameEntry.UI.CloseUIForm(UIForm);});
        SelfDataManager.Instance.CurMazeItemList ??= new();
        SelfDataManager.Instance.CurMazeItemList.Clear();
        _pointPool ??= new ObjectPool<GameObject>(() =>
        {
            GameObject ob = Instantiate(_pointTemp, _invisbleParent.transform);
            MazePointItem mp = ob.GetComponent<MazePointItem>();
            if (mp != null)
            {
                SelfDataManager.Instance.CurMazeItemList.Add(mp);
                mp.OnClickPointCallback = OnClickPoint;
            }
            return ob;
        }, (obj) => {obj.SetActive(true); obj.transform.SetParent(_showPointParent.transform); }, (obj) => {obj.transform.SetParent(_invisbleParent.transform);}, Destroy);
        
        _linePool ??= new ObjectPool<GameObject>(() =>
        {
            GameObject ob = Instantiate(_lineTemp, _invisbleParent.transform);
            return ob;
        }, (obj) => {obj.SetActive(true); obj.transform.SetParent(_showLineParent.transform); }, (obj) => {obj.transform.SetParent(_invisbleParent.transform);}, Destroy);

        var mazeList = SelfDataManager.Instance.CurMazeList;
        if (mazeList == null)
        {
            return;
        }
        
        foreach (var onePointData in mazeList)
        {
            if (onePointData.CurType == MazePointType.Empty)
            {
                continue;
            }
            var oneNewPoint = _pointPool.Get();
            MazePointItem mp = oneNewPoint.GetComponent<MazePointItem>();
            oneNewPoint.transform.position = ItemStartPos + new Vector2(onePointData.Pos.x * ItemOffSet.x, onePointData.Pos.y * ItemOffSet.y);
            mp.Pos = onePointData.Pos;
            mp.Name.text = onePointData.CurType.ToString();
            mp.IsPassImg.SetActive(false);
            
            foreach (var linkPointData in onePointData.LinkPoint)
            {
                if (linkPointData.Pos.x > onePointData.Pos.x || (linkPointData.Pos.x == onePointData.Pos.x&&linkPointData.Pos.y > onePointData.Pos.y))
                {
                    Vector3 linkPosition = ItemStartPos + new Vector2(linkPointData.Pos.x * ItemOffSet.x, linkPointData.Pos.y * ItemOffSet.y);
                    var oneNewLine = _linePool.Get();
                    var position = oneNewPoint.transform.position;
                    Vector3 direction = position - linkPosition;
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    oneNewLine.transform.rotation = Quaternion.Euler(0, 0, angle);
                    oneNewLine.transform.position = (position + linkPosition) / 2;
                }
            }
        }
        InitFog();
        GameEntry.Event.Subscribe(MapFreshEventArgs.EventId,OnMapFresh);
    }

    private void InitFog()
    {
        // 创建一张maskSize x maskSize的纹理
        _maskTexture = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, false);
        FreshFog();
    }

    public void FreshFog()
    {
        var mazeList = SelfDataManager.Instance.CurMazeList;
        // 填充白色
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                Color color = Color.white;
                _maskTexture.SetPixel(x, y, color);
            }
        }
        var screenWidth = Screen.width;
        var screenHeight = Screen.height;
        var screenPosDir = new Vector2(screenWidth / 2.0f, screenHeight / 2.0f);
        //对所有可见的point都改为black
        foreach (var point in mazeList)
        {
            if (point.CanSee)
            {
                var localPos = ItemStartPos + new Vector2(point.Pos.x * ItemOffSet.x, point.Pos.y * ItemOffSet.y);
                var screenPos = localPos + screenPosDir;
                //获取对应位置的 mask 像素点xy
                var maskXY = new Vector2Int((int)(screenPos.x * mapWidth / screenWidth), (int)(screenPos.y * mapHeight/screenHeight));
                var startX = Math.Max(0, maskXY.x - onePointFarRadius);
                var startY = Math.Max(0, maskXY.y - onePointFarRadius);
                var endX = Math.Min(mapWidth, maskXY.x + onePointFarRadius);
                var endY = Math.Min(mapHeight, maskXY.y + onePointFarRadius);
                for (int maskTextureX = startX; maskTextureX < endX; maskTextureX++)
                {
                    for (int maskTextureY = startY; maskTextureY < endY; maskTextureY++)
                    {
                        if ((maskTextureX - maskXY.x) * (maskTextureX - maskXY.x) + (maskTextureY - maskXY.y) * (maskTextureY - maskXY.y) < onePointFarRadius * onePointFarRadius)
                        {
                            _maskTexture.SetPixel(maskTextureX, maskTextureY, Color.black);
                        }
                    }
                }
            }
        }
        // 应用更改
        _maskTexture.Apply();
        // 将生成的纹理传递给材质
        if (_fogImage != null&&_fogImage.material!=null)
        {
            _fogImage.material.SetTexture("_MaskTex", _maskTexture);
        }
    }
    private void OnClickPoint(MazePointItem item)
    {
        Log.Info(item.Pos);
        var point=SelfDataManager.Instance.GetPoint(item.Pos.x, item.Pos.y);
        GameEntry.Event.Fire(this,EnterPointEventArgs.Create(point));
        //point.CanSee = true;
        //FreshFog();
    }

    private void FreshMazePointItem()
    {
        var mazeItemList = SelfDataManager.Instance.CurMazeItemList;
        foreach (var curItem in mazeItemList)
        {
            var point=SelfDataManager.Instance.GetPoint(curItem.Pos.x, curItem.Pos.y);
            if (point.CurPassState==MazePoint.PointPassState.Pass)
            {
                curItem.IsPassImg.SetActive(true);
            }
        }
    }
    public void OnMapFresh(object sender,GameEventArgs e)
    {
        MapFreshEventArgs ne = (MapFreshEventArgs)e;
        if (ne == null)
        {
            return;
        }
        FreshMazePointItem();
        FreshFog();
    }
}
