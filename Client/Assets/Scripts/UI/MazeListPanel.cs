using System.Collections;
using System.Collections.Generic;
using Maze;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class MazeListPanelCtrl : UIFormLogic
{
    [SerializeField] private GameObject _pointTemp;
    [SerializeField] private GameObject _lineTemp;
    [SerializeField] private GameObject _invisbleParent;
    [SerializeField] private GameObject _showPointParent;
    [SerializeField] private GameObject _showLineParent;
    private ObjectPool<GameObject> _pointPool;
    private ObjectPool<GameObject> _linePool;
    [SerializeField] private Button _btnClose;
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnClose.onClick.AddListener(() => { GameEntry.UI.CloseUIForm(UIForm);});
        _pointPool ??= new ObjectPool<GameObject>(() =>
        {
            GameObject ob = Instantiate(_pointTemp, _invisbleParent.transform);
            MazePointItem mp = ob.GetComponent<MazePointItem>();
            if (mp != null)
            {
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

        Vector2 startPos = new Vector2(-500, -500);
        Vector2 offSet = new Vector2(150, 150);
        foreach (var onePointData in mazeList)
        {
            var oneNewPoint = _pointPool.Get();
            MazePointItem mp = oneNewPoint.GetComponent<MazePointItem>();
            oneNewPoint.transform.position = startPos + new Vector2(onePointData.Pos.x * offSet.x, onePointData.Pos.y * offSet.y);
            mp.Pos = onePointData.Pos;
            mp.Name.text = onePointData.CurType.ToString();
            foreach (var linkPointData in onePointData.LinkPoint)
            {
                if (linkPointData.Pos.x > onePointData.Pos.x || linkPointData.Pos.y > onePointData.Pos.y)
                {
                    Vector3 linkPosition = startPos + new Vector2(linkPointData.Pos.x * offSet.x, linkPointData.Pos.y * offSet.y);
                    var oneNewLine = _linePool.Get();
                    var position = oneNewPoint.transform.position;
                    Vector3 direction = position - linkPosition;
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    oneNewLine.transform.rotation = Quaternion.Euler(0, 0, angle);
                    oneNewLine.transform.position = (position + linkPosition) / 2;
                }
            }
        }
    }

    private void OnClickPoint(MazePointItem item)
    {
        Log.Info(item.Pos);
    }
}
