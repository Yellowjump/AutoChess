using liuchengguanli;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class UIguanli : UIFormLogic
{
    [SerializeField]
    private Button _btnShuaxin;
    [SerializeField]
    private Button _btnShouqi;
    [SerializeField]
    private Button _btnOne;
    [SerializeField]
    private Button _btnTwo;
    [SerializeField]
    private Button _btnThree;
    [SerializeField]
    private Button _btnFour;
    [SerializeField]
    private Button _btnFive;
    [SerializeField]
    private Canvas _cavJiemian;
    private float lerpTime=0;
    private float Y = 0;

    [SerializeField]
    private AnimationCurve MoveInCurve;

    [SerializeField]
    private Text SqOrFx;//显示收起还是放下

    [SerializeField]
    private Text JinBi;//显示金币数
    
    private int ShouqiOrFangxia = -1;//-1是收起，1是放下

    QiziGuanLi qiziguanli;
    Jinqian jinqian=new Jinqian();//创建一个金钱类对象
    

    protected override void OnInit(object userData)
    {
        JinBi.text = jinqian.GetJinBiNum().ToString();
        transform.position = Vector3.zero;
        _btnShuaxin.onClick.AddListener(OnClickBtnShuaxin);
        _btnShouqi.onClick.AddListener(OnClickBtnShouqi);
        //棋子购买按键框
        _btnOne.onClick.AddListener(OnClickBtnGouMaiOne);
        _btnTwo.onClick.AddListener(OnClickBtnGouMaiTwo);
        _btnThree.onClick.AddListener(OnClickBtnGouMaiThree);
        _btnFour.onClick.AddListener(OnClickBtnGouMaiFour);
        _btnFive.onClick.AddListener(OnClickBtnGouMaiFive);


        base.OnInit(userData);
    }

    private void OnClickBtnShouqi()
    {
        
        //JinBi.text = jinqian.GetJinBiNum().ToString();
        if (ShouqiOrFangxia == -1)
        {
            SqOrFx.text = "收起";
        }
        else
        {
            SqOrFx.text = "放下";
        }
        //_cavJiemian.transform.AddLocalPositionY(300) ;
        //Log.Info("TestBtn OnClick");
        
        
        StopCoroutine(ChuangkouYidong());
        lerpTime = 0;

        StartCoroutine(ChuangkouYidong());
        ShouqiOrFangxia = -ShouqiOrFangxia;
    }
    private void OnClickBtnShuaxin()
    {
        if (jinqian.GetJinBiNum()>=2)
        {
            jinqian.changejinqian(-2);
            JinBi.text = jinqian.GetJinBiNum().ToString();
            //刷新UI棋子购买界面
        }
        //Log.Info("TestBtn OnClick");
    }
    IEnumerator ChuangkouYidong()
    {
        var rectTrans = _cavJiemian.transform as RectTransform;
        Y = rectTrans.anchoredPosition.y;
        while (lerpTime <40)
        {
            lerpTime++;
            rectTrans.anchoredPosition = new Vector2(rectTrans.anchoredPosition.x, (Mathf.Lerp(Y, -150*ShouqiOrFangxia, lerpTime/40)));
            //Log.Info("hfk nowY : " + MoveInCurve.Evaluate(lerpTime) + " lerptime: " + lerpTime + " Lerp: " + Mathf.Lerp(rectTrans.anchoredPosition.y, -150 * ShouqiOrFangxia, lerpTime / 40) + " y: " + rectTrans.anchoredPosition.y);
            yield return null;
        }
        //rectTrans.anchoredPosition = new Vector2(rectTrans.anchoredPosition.x, 150);
        yield return null;
        StopCoroutine(ChuangkouYidong());
    }
    private void OnClickBtnGouMaiOne()//购买第一个格子里的棋子
    {
        PoolObject.instance.Pool[0].Get();
    }
    private void OnClickBtnGouMaiTwo()
    {

    }
    private void OnClickBtnGouMaiThree()
    {

    }
    private void OnClickBtnGouMaiFour()
    {

    }
    private void OnClickBtnGouMaiFive()
    {

    }
}
