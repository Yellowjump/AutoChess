using System;
using System.Collections;
using System.Collections.Generic;
using GameFramework;
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
        _curMazeItemList ??= ListPool<AreaPointItem>.Get();
        _curMazeItemList.Clear();
        _curLineList ??= ListPool<Image>.Get();
        _curLineList.Clear();
        _pointPool ??= new ObjectPool<GameObject>(() =>
        {
            GameObject ob = Instantiate(_pointTemp, GameEntry.HeroManager.WorldCanvas.transform);
            return ob;
        }, (obj) => {obj.SetActive(true); obj.transform.SetParent(GameEntry.HeroManager.WorldCanvas.transform); }, (obj) => {obj.transform.SetParent(GameEntry.HeroManager.DisableRoot);}, Destroy);
        
        _linePool ??= new ObjectPool<GameObject>(() =>
        {
            GameObject ob = Instantiate(_lineTemp, GameEntry.HeroManager.DisableRoot);
            return ob;
        }, (obj) => {obj.SetActive(true); obj.transform.SetParent(GameEntry.HeroManager.WorldCanvas.transform); }, (obj) => {obj.transform.SetParent(GameEntry.HeroManager.DisableRoot);}, Destroy);
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        GenLineAndPoint();
        GameEntry.Event.Subscribe(MapFreshEventArgs.EventId,OnMapFresh);
        GameEntry.Event.Subscribe(MapFreshOpaqueEventArgs.EventId,OnMapOpaqueFresh);
        FreshMazePointItem();
    }

    private void GenLineAndPoint()
    {
        var mazeList = GameEntry.HeroManager.CurAreaList;
        if (mazeList == null)
        {
            return;
        }
        //先生成线
        foreach (var onePointData in mazeList)
        {
            if (onePointData.CurType == MazePointType.Empty)
            {
                continue;
            }
            foreach (var linkPointIndex in onePointData.LinkPointList)
            {
                var linkPointData = GameEntry.HeroManager.GetPoint(linkPointIndex);
                if (linkPointData.Pos.x > onePointData.Pos.x || (Math.Abs(linkPointData.Pos.x - onePointData.Pos.x) < float.Epsilon &&linkPointData.Pos.y > onePointData.Pos.y))
                {
                    Vector3 linkPosition = linkPointData.Pos;
                    var oneNewLine = _linePool.Get();
                    var position = onePointData.Pos;
                    Vector3 direction = position - linkPosition;
                    oneNewLine.transform.position = (position + linkPosition) / 2;
                    var upward = Vector3.Cross(Vector3.up, direction);
                    var forward = Vector3.Cross(upward, direction);
                    oneNewLine.transform.rotation = Quaternion.LookRotation(forward,upward);
                    Image image = oneNewLine.GetComponent<Image>();
                    image.rectTransform.sizeDelta = new Vector2(direction.magnitude, 20);
                    _curLineList.Add(image);
                }
            }
        }
        //再生成点
        foreach (var onePointData in mazeList)
        {
            if (onePointData.CurType == MazePointType.Empty)
            {
                continue;
            }
            var oneNewPoint = _pointPool.Get();
            AreaPointItem mp = oneNewPoint.GetComponent<AreaPointItem>();
            if (mp != null)
            {
                _curMazeItemList.Add(mp);
                mp.OnClickPointCallback = OnClickPoint;
                mp.Init();
            }
            mp.GetBgImg(4800+(int)onePointData.CurType);
            oneNewPoint.transform.position = onePointData.Pos;
            mp.Index = onePointData.Index;
            mp.Name.text = onePointData.CurType.ToString();
            mp.IsPassImg.SetActive(false);
            if (onePointData.CurPassState==AreaPoint.PointPassState.Pass)
            {
                mp.IsPassImg.SetActive(true);
            }
            mp.BtnClick.interactable = onePointData.CurPassState == AreaPoint.PointPassState.Unlock;
        }
    }
    private void OnClickPoint(AreaPointItem item)
    {
        Log.Info(item.Index);
        var point=GameEntry.HeroManager.GetPoint(item.Index);
        GameEntry.Event.Fire(this,EnterPointEventArgs.Create(point));
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
            var point=GameEntry.HeroManager.GetPoint(curItem.Index);
            curItem.SetOpaque(1);
            curItem.gameObject.SetActive(true);
            curItem.IsPassImg.SetActive(point.CurPassState==AreaPoint.PointPassState.Pass);
            curItem.BtnClick.interactable = point.CurPassState == AreaPoint.PointPassState.Unlock;
        }
        foreach (var oneImage in _curLineList)
        {
            oneImage.color= new Color(oneImage.color.r,oneImage.color.g,oneImage.color.b,1);
            oneImage.gameObject.SetActive(true);
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

    public override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        GameEntry.Event.Unsubscribe(MapFreshEventArgs.EventId,OnMapFresh);
        GameEntry.Event.Unsubscribe(MapFreshOpaqueEventArgs.EventId,OnMapOpaqueFresh);
        if (_curMazeItemList != null)
        {
            foreach (var oneItem in _curMazeItemList)
            {
                _pointPool.Release(oneItem.gameObject);
            }
            _curMazeItemList.Clear();
        }
        if (_curLineList != null)
        {
            foreach (var oneItem in _curLineList)
            {
                _linePool.Release(oneItem.gameObject);
            }
            _curLineList.Clear();
        }
    }
}
