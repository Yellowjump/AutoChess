using System;
using System.Collections;
using System.Collections.Generic;
using GameFramework.Event;
using Maze;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using SelfEventArg;
using GameFramework.Resource;
using UnityEngine.Rendering.Universal;

public class AreaListPanel3DCtrl : UIFormLogic
{
    private List<AreaPointItem> _curMazeItemList;
    private List<Image> _curLineList;
    [SerializeField] private GameObject _pointTemp;
    [SerializeField] private GameObject _lineTemp;
    [SerializeField] private Vector2 ItemStartPos = new Vector2(-500, -500);
    [SerializeField] private Vector2 ItemOffSet = new Vector2(150, 150);
    private ObjectPool<GameObject> _pointPool;
    private ObjectPool<GameObject> _linePool;
    [SerializeField] private Button _btnClose;
    //-----------fogStart-------------------
    /*[SerializeField] private RawImage _fogImage;
    [SerializeField] private int mapWidth=50;
    [SerializeField] private int mapHeight=50;
    [SerializeField] private int onePointFarRadius = 20;*/
    //public Color32[] colorBuffer;//r装当前是否可见的透明度
    private RenderBuffer FogTargetBuffer;//放入image中的renderBuffer;
    private Texture2D _maskTexture;//生成的透明度mask
    //-----------fogEnd
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnClose.onClick.AddListener(() => { GameEntry.UI.CloseUIForm(UIForm);});
        _curMazeItemList ??= ListPool<AreaPointItem>.Get();
        _curMazeItemList.Clear();
        _curLineList ??= ListPool<Image>.Get();
        _pointPool ??= new ObjectPool<GameObject>(() =>
        {
            GameObject ob = Instantiate(_pointTemp, GameEntry.HeroManager.WorldCanvas.transform);
            AreaPointItem mp = ob.GetComponent<AreaPointItem>();
            if (mp != null)
            {
                _curMazeItemList.Add(mp);
                mp.OnClickPointCallback = OnClickPoint;
                mp.Init();
            }
            return ob;
        }, (obj) => {obj.SetActive(true); obj.transform.SetParent(GameEntry.HeroManager.WorldCanvas.transform); }, (obj) => {obj.transform.SetParent(GameEntry.HeroManager.DisableRoot);}, Destroy);
        
        _linePool ??= new ObjectPool<GameObject>(() =>
        {
            GameObject ob = Instantiate(_lineTemp, GameEntry.HeroManager.DisableRoot);
            return ob;
        }, (obj) => {obj.SetActive(true); obj.transform.SetParent(GameEntry.HeroManager.WorldCanvas.transform); }, (obj) => {obj.transform.SetParent(GameEntry.HeroManager.DisableRoot);}, Destroy);

        var mazeList = SelfDataManager.Instance.CurAreaList;
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
            AreaPointItem mp = oneNewPoint.GetComponent<AreaPointItem>();
            mp.GetBgImg(4800+(int)onePointData.CurType);
            oneNewPoint.transform.position = onePointData.Pos;
            mp.Index = onePointData.Index;
            mp.Name.text = onePointData.CurType.ToString();
            mp.IsPassImg.SetActive(false);
            if (onePointData.CurPassState==AreaPoint.PointPassState.Pass)
            {
                mp.IsPassImg.SetActive(true);
            }
            foreach (var linkPointIndex in onePointData.LinkPointList)
            {
                var linkPointData = SelfDataManager.Instance.GetPoint(linkPointIndex);
                if (linkPointData.Pos.x > onePointData.Pos.x || (Math.Abs(linkPointData.Pos.x - onePointData.Pos.x) < float.Epsilon &&linkPointData.Pos.y > onePointData.Pos.y))
                {
                    Vector3 linkPosition = linkPointData.Pos;
                    var oneNewLine = _linePool.Get();
                    var position = oneNewPoint.transform.position;
                    Vector3 direction = position - linkPosition;
                    float angle = Mathf.Atan2(direction.z,direction.x) * Mathf.Rad2Deg;
                    oneNewLine.transform.rotation = Quaternion.Euler(90, 0,angle);
                    oneNewLine.transform.position = (position + linkPosition) / 2;
                    //oneNewLine.transform.localScale=new Vector3(direction.magnitude,20,direction.magnitude);
                    Image image = oneNewLine.GetComponent<Image>();
                    //image.rectTransform.rect.width = direction.magnitude;
                    image.rectTransform.sizeDelta = new Vector2(direction.magnitude, 20);
                    _curLineList.Add(image);
                    //oneNewLine.transform.SetLocalScaleX(direction.magnitude);
                }
            }
        }
        InitFog();
        GameEntry.Event.Subscribe(MapFreshEventArgs.EventId,OnMapFresh);
        GameEntry.Event.Subscribe(MapFreshOpaqueEventArgs.EventId,OnMapOpaqueFresh);
    }

    private void InitFog()
    {
        // 创建一张maskSize x maskSize的纹理
        /*_maskTexture = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, false);*/
        FreshFog();
    }

    public void FreshFog()
    {
        /*var mazeList = SelfDataManager.Instance.CurMazeList;
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
                var localPos = ItemStartPos + new Vector2(point.PosObsolete.x * ItemOffSet.x, point.PosObsolete.y * ItemOffSet.y);
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
        }*/
    }
    private void OnClickPoint(AreaPointItem item)
    {
        Log.Info(item.Index);
        var point=SelfDataManager.Instance.GetPoint(item.Index);
        GameEntry.Event.Fire(this,EnterPointEventArgs.Create(point));
        //point.CanSee = true;
        //FreshFog();
    }

    private void FreshMazePointItem()
    {
        var mazeItemList = _curMazeItemList;
        foreach (var curItem in mazeItemList)
        {
            if (curItem == null)
            {
                continue;
            }
            var point=SelfDataManager.Instance.GetPoint(curItem.Index);
            if (point.CurPassState==AreaPoint.PointPassState.Pass)
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
    public void OnMapOpaqueFresh(object sender,GameEventArgs e)
    {
        MapFreshOpaqueEventArgs ne = (MapFreshOpaqueEventArgs)e;
        if (ne == null)
        {
            return;
        }

        foreach (var oneImage in _curLineList)
        {
            oneImage.color = new Color(oneImage.color.r,oneImage.color.g,oneImage.color.b,ne.Opacity);
            oneImage.gameObject.SetActive(ne.Opacity>0);
        }

        foreach (var oneMazeItem in _curMazeItemList)
        {
            oneMazeItem.SetOpaque(ne.Opacity);
            oneMazeItem.gameObject.SetActive(ne.Opacity>0);
        }
    }
}
