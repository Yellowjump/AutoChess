using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class MazePointItem:MonoBehaviour
{
    public Vector2Int Pos = Vector2Int.zero;
    public TextMeshProUGUI Name;
    public GameObject IsPassImg;
    public Image bgImg;
    public Button BtnClick;
    public Action<MazePointItem> OnClickPointCallback;
    private void Start()
    {
        BtnClick.onClick.AddListener(OnClickBtn);
    }

    private void OnClickBtn()
    {
        OnClickPointCallback.Invoke(this);
    }
}