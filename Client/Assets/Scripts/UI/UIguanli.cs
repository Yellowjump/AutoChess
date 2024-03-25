using liuchengguanli;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
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
    private Button _btnShengji;

    [SerializeField]
    private Slider _slderDengji;
    [SerializeField]
    private Canvas _cavJiemian;
    private float lerpTime=0;
    private float Y = 0;

    [SerializeField]
    private AnimationCurve MoveInCurve;

    [SerializeField]
    private Text SqOrFx;//显示收起还是放下
    private int ShouqiOrFangxia = -1;//-1是收起，1是放下

    [SerializeField]
    private Text JinBi;//显示金币数

    [SerializeField]
    private Text DengJi;//显示等级

    Jinqian jinqian=new Jinqian();//创建一个金钱类对象
    
    private List<Sprite>ListQiziSprite = new List<Sprite>();//保存棋子图片
    protected override void OnInit(object userData)
    {
        JinBi.text = jinqian.GetJinBiNum().ToString();
        DengJi.text = "等级："+Dengji.Instance.getDj().ToString()+"\n"+ Dengji.Instance.jinyan+" / " + Dengji.Instance.shengjixuqiu[Dengji.Instance.getDj()];
        transform.position = Vector3.zero;
        _btnShuaxin.onClick.AddListener(OnClickBtnShuaxin);//刷新按键
        _btnShouqi.onClick.AddListener(OnClickBtnShouqi);//收起放下按键
        //棋子购买按键框1-5
        _btnOne.onClick.AddListener(OnClickBtnGouMaiOne);
        _btnTwo.onClick.AddListener(OnClickBtnGouMaiTwo);
        _btnThree.onClick.AddListener(OnClickBtnGouMaiThree);
        _btnFour.onClick.AddListener(OnClickBtnGouMaiFour);
        _btnFive.onClick.AddListener(OnClickBtnGouMaiFive);
        _btnShengji.onClick.AddListener(OnClickBtnShengJi);
        InitQiziPicture();//保存棋子购买界面图片，使用的时候需注意ListQiziPicture[qiziindex];
        //初始先加两块，再刷新
        jinqian.changejinqian(2);
        OnClickBtnShuaxin();


        base.OnInit(userData);
    }
    private void InitQiziPicture()
    {
        Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Image/qizi_0.jpg");
        //_btnOne.image.sprite = img;
        //依次存入img
        ListQiziSprite.Add(spr);
        //
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
        StopCoroutine(ChuangkouYidong());
        lerpTime = 0;
        StartCoroutine(ChuangkouYidong());
        ShouqiOrFangxia = -ShouqiOrFangxia;
    }
    private void OnClickBtnShuaxin()
    {
        if (jinqian.GetJinBiNum()>=2)
        {
            //先扣钱
            jinqian.changejinqian(-2);
            JinBi.text = jinqian.GetJinBiNum().ToString();
            //获取5个随机qiziIndex
            //Log.Info("hfk:"+QiziGuanLi.Instance.paiku1num);
            QiziGuanLi.Instance.choupai(Dengji.Instance.getDj());
            //激活5个购买按钮
            _btnOne.gameObject.SetActive(true);
            _btnTwo.gameObject.SetActive(true);
            _btnThree.gameObject.SetActive(true);
            _btnFour.gameObject.SetActive(true);
            _btnFive.gameObject.SetActive(true);
            //刷新UI棋子购买界面
            _btnOne.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziIndex[0]];
            _btnTwo.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziIndex[1]];
            _btnThree.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziIndex[2]];
            _btnFour.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziIndex[3]];
            _btnFive.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziIndex[4]];
        }
        //Log.Info("TestBtn OnClick");
    }
    private void OnClickBtnShengJi()
    {
        int dj = Dengji.Instance.getDj();
        if (jinqian.GetJinBiNum() >= 4 && dj < 9)
        {
            //先扣钱
            jinqian.changejinqian(-4);
            JinBi.text = jinqian.GetJinBiNum().ToString();
            //涨经验
            Dengji.Instance.changejinyan(4);
            DengJi.text = "等级：" + Dengji.Instance.getDj().ToString() + "\n" + Dengji.Instance.jinyan + " / " + Dengji.Instance.shengjixuqiu[Dengji.Instance.getDj()];
            _slderDengji.value = (float)Dengji.Instance.jinyan / (float)Dengji.Instance.shengjixuqiu[Dengji.Instance.getDj()];
        }
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
        int index = QiziGuanLi.Instance.goumaiUIqiziIndex[0];
        int paikuindex = QiziGuanLi.Instance.goumaiUIqiziPaikuIndex[0];
        goumaiqizi(index, paikuindex, _btnOne);
    }
    private void OnClickBtnGouMaiTwo()
    {
        int index = QiziGuanLi.Instance.goumaiUIqiziIndex[1];
        int paikuindex = QiziGuanLi.Instance.goumaiUIqiziPaikuIndex[1];
        goumaiqizi(index, paikuindex, _btnTwo);

    }
    private void OnClickBtnGouMaiThree()
    {
        int index = QiziGuanLi.Instance.goumaiUIqiziIndex[2];
        int paikuindex = QiziGuanLi.Instance.goumaiUIqiziPaikuIndex[2];
        goumaiqizi(index, paikuindex, _btnThree);
    }
    private void OnClickBtnGouMaiFour()
    {
        int index = QiziGuanLi.Instance.goumaiUIqiziIndex[3];
        int paikuindex = QiziGuanLi.Instance.goumaiUIqiziPaikuIndex[3];
        goumaiqizi(index, paikuindex, _btnFour);
    }
    private void OnClickBtnGouMaiFive()
    {
        int index = QiziGuanLi.Instance.goumaiUIqiziIndex[4];
        int paikuindex = QiziGuanLi.Instance.goumaiUIqiziPaikuIndex[4];
        goumaiqizi(index, paikuindex, _btnFive);
    }
    private void goumaiqizi(int index,int paikuIndex,Button btn)
    {
        int kwCx = QiziGuanLi.Instance.findkongweiCX();
        int feiyong = QiziGuanLi.Instance.qizi[index];
        if (kwCx != -1 && jinqian.GetJinBiNum() >= feiyong)//先场下有位置，再买得起
        {
            QiziGuanLi.Instance.goumaiqizi(index,paikuIndex, kwCx,feiyong);
            EntityQizi qizi = Pool.instance.PoolEntity.Get() as EntityQizi;
            qizi.Init(index);
            qizi.GObj.transform.localPosition = new Vector3(-4 + kwCx, 0, -4.5f);
            jinqian.changejinqian(-feiyong);
            JinBi.text = jinqian.GetJinBiNum().ToString();
            btn.gameObject.SetActive(false);
        }
    }

}
