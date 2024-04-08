using GameFramework.Fsm;
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
    public Button _btnChushou;

    [SerializeField]
    private Slider _slderDengji;//显示经验升级进度
    [SerializeField]
    public Slider _slderJindu;//显示流程进度

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

    [SerializeField]
    private Text ChuSouNum;//显示出售价格

    [SerializeField]
    private Image qizishuxin;//显示棋子属性面板
    private EntityQizi shuxin__qizi;
    [SerializeField]
    private Text xuetiaonow;//显示血量
    [SerializeField]
    private Text xuetiaosum;
    [SerializeField]
    public Slider _slderXuetiao;//显示血条
    [SerializeField]
    private Text pownow;//显示蓝量
    [SerializeField]
    private Text powsum;
    [SerializeField]
    public Slider _slderPow;//显示蓝条

    Jinqian jinqian=new Jinqian();//创建一个金钱类对象
    //拖拽棋子相关
    private bool GetOrNotGetQizi = false;
    GameObject qiziobj;
    EntityQizi qizi;
    EntityQizi qiziother;
    Vector3 qiziObj_oldlocation;

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
        //初始将按钮和函数绑定，再将出售界面取消激活
        _btnChushou.onClick.AddListener(OnClickBtnChushou);
        _btnChushou.gameObject.SetActive(false);
        
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
            if (QiziGuanLi.Instance.goumaiUIqiziIndex[0] > -1)
            {
                _btnOne.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziIndex[0]];
            }
            if (QiziGuanLi.Instance.goumaiUIqiziIndex[1] > -1)
            {
                _btnTwo.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziIndex[1]];
            }
            if (QiziGuanLi.Instance.goumaiUIqiziIndex[2] > -1)
            {
                _btnThree.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziIndex[2]];
            }
            if (QiziGuanLi.Instance.goumaiUIqiziIndex[3] > -1)
            {
                _btnFour.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziIndex[3]];
            }
            if (QiziGuanLi.Instance.goumaiUIqiziIndex[4] > -1)
            {
                _btnFive.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziIndex[4]];
            } 
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

        if (jinqian.GetJinBiNum() >= feiyong)//先场下有位置，再买得起
        {
            int SJ = shengji(index);
            if (SJ == -1 && kwCx != -1)//没升级棋子但是有空位
            {
                QiziGuanLi.Instance.goumaiqizi(index, paikuIndex, kwCx, feiyong);
                qizi = Pool.instance.PoolEntity.Get() as EntityQizi;
                qizi.Init(index);

                qizi.GObj.transform.localPosition = new Vector3(-4 + kwCx, 0, -4.5f);
                jinqian.changejinqian(-feiyong);
                JinBi.text = jinqian.GetJinBiNum().ToString();
                btn.gameObject.SetActive(false);
            }
            else if (SJ==0)//购买后能升级棋子不用有空位
            {
                jinqian.changejinqian(-feiyong);
                JinBi.text = jinqian.GetJinBiNum().ToString();
                btn.gameObject.SetActive(false);
            }
        }
    }

    private void OnClickBtnChushou()
    {
        //Log.Info("hfk"+111);
        if (qiziObj_oldlocation.z != -4.5)//棋子在场上
        {
            QiziGuanLi.Instance.QiziCSList.Remove(qizi);

        }
        else //棋子在场下
        {
            QiziGuanLi.Instance.changxia[(int)qiziObj_oldlocation.x + 4] = -1;
        }
        QiziGuanLi.Instance.QiziList.Remove(qizi);
        int jq = qizi.money;//获取棋子的金额
        jinqian.changejinqian(jq);//卖完棋子之后加钱，并把棋子放回池子里,牌库也要更新
        JinBi.text = jinqian.GetJinBiNum().ToString();
        QiziGuanLi.Instance.chushouQizi(qizi.Index, jq,qizi.level);
        Pool.instance.PoolObject[qizi.Index].Release(qizi.GObj);
        Pool.instance.PoolEntity.Release(qizi);

    }
    private void shuxinxianshi(EntityQizi qz)
    {
        xuetiaonow.text = qizi.xueliangnow.ToString();
        xuetiaosum.text = qizi.xueliangsum.ToString();
        pownow.text = qizi.powernow.ToString();
        powsum.text = qizi.powersum.ToString();
        _slderXuetiao.value = qizi.xueliangnow /qizi.xueliangsum;
        _slderPow.value = qizi.powernow /qizi.powersum;
    }
    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (!GetOrNotGetQizi)
        {
            if (Input.GetMouseButtonDown(1))//显示棋子属性面板
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit) && hit.transform.tag == "qizi")
                {
                    for (int i = 0; i < QiziGuanLi.Instance.QiziList.Count; i++)
                    {
                        if (hit.transform.localPosition == QiziGuanLi.Instance.QiziList[i].GObj.transform.localPosition)
                        {
                            shuxin__qizi = QiziGuanLi.Instance.QiziList[i];
                            break;
                        }
                    }
                    //Log.Info("hfk:" + hit.transform.tag);
                    qizishuxin.gameObject.SetActive(true);
                    shuxinxianshi(shuxin__qizi);
                }
            }
            if (Input.GetMouseButtonDown(0))
            {
                qizishuxin.gameObject.SetActive(false);
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit) && hit.transform.tag == "qizi"&&hit.transform.localPosition.z<0)
                {
                    for (int i = 0; i < QiziGuanLi.Instance.QiziList.Count; i++)
                    {
                        if (hit.transform.localPosition == QiziGuanLi.Instance.QiziList[i].GObj.transform.localPosition)
                        {
                            qizi = QiziGuanLi.Instance.QiziList[i];
                            break;
                        }
                    }
                    //Log.Info("hfk:" + hit.transform.tag);
                    qiziobj = hit.collider.gameObject;
                    qiziObj_oldlocation = qiziobj.transform.position;
                    GetOrNotGetQizi = true;
                    _btnChushou.gameObject.SetActive(true);
                    ChuSouNum.text = "出售\n"+qizi.money+"金币";
                }
            }
        }
        else //if (timenow <= Time.time)
        {
            //timenow = Time.time + 0.1f;
            int findotherQizi = -1;
            int findzijiqizi = -1;
            int findqige = -1;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray);
            for (int i = 0; i < hits.Length; i++)//移动鼠标拖拽棋子
            {
                if (hits[i].transform.tag == "qige")
                {
                    qiziobj.transform.localPosition = hits[i].point + new Vector3(0, 0.2f, 0);
                    findqige = i;
                }
                else if (hits[i].transform.name == "qipan")
                {
                    qiziobj.transform.localPosition = hits[i].point + new Vector3(0, 0.2f, 0);
                }
                if (hits[i].transform.tag == "qizi" && hits[i].transform != qiziobj.transform)
                {
                    findotherQizi = i;
                }
                if (hits[i].transform == qiziobj.transform)
                {
                    findzijiqizi = i;
                }
            }
            if (Input.GetMouseButtonDown(0))//判断射线检测，应该放下棋子到棋格子上还是和已经放置的棋子交换位置，或者无法放置棋子（回到原位）
            {
                if (findqige != -1 && hits[findqige].transform.position.z<0)//鼠标不在棋格上，放回原位
                {
                    if (findotherQizi == -1)//空格子
                    {
                        //如果拖拽的棋子是场下的
                        if (qiziObj_oldlocation.z == -4.5)
                        {   //放的位置是场上
                            if (hits[findqige].transform.localPosition.z != -4.5)
                            {   //并且场上棋子数小于等于等级，需要将changxia[]所处位置置为-1
                                if (QiziGuanLi.Instance.QiziCSList.Count < Dengji.Instance.getDj())
                                {
                                    qiziobj.transform.localPosition = hits[findqige].transform.localPosition;
                                    QiziGuanLi.Instance.changxia[(int)qiziObj_oldlocation.x + 4] = -1;
                                    QiziGuanLi.Instance.QiziCSList.Add(qizi);
                                }
                                else
                                {
                                    qiziobj.transform.localPosition = qiziObj_oldlocation;
                                }
                            }
                            else //放的位置是场下
                            {
                                qiziobj.transform.localPosition = hits[findqige].transform.localPosition;
                                QiziGuanLi.Instance.changxia[(int)qiziObj_oldlocation.x + 4] = -1;
                                QiziGuanLi.Instance.changxia[(int)hits[findqige].transform.position.x + 4] = qizi.Index;
                            }
                        }
                        else//如果棋子是场上的
                        {   //如果放的位置是场下
                            if (hits[findqige].transform.localPosition.z == -4.5)
                            {
                                QiziGuanLi.Instance.changxia[(int)hits[findqige].transform.localPosition.x + 4] = qizi.Index;
                                QiziGuanLi.Instance.QiziCSList.Remove(qizi);
                            }
                            qiziobj.transform.localPosition = hits[findqige].transform.localPosition;
                        }
                    }
                    else//棋格上有别的棋子
                    {
                        for (int i = 0; i < QiziGuanLi.Instance.QiziList.Count; i++)
                        {
                            if (hits[findotherQizi].transform.localPosition == QiziGuanLi.Instance.QiziList[i].GObj.transform.localPosition)
                            {
                                qiziother = QiziGuanLi.Instance.QiziList[i];
                            }
                        }
                        //如果拖拽的棋子是场下的
                        if (qiziObj_oldlocation.z == -4.5)
                        {   //放的棋子位置是场上
                            if (hits[findqige].transform.localPosition.z != -4.5)
                            {
                                qiziobj.transform.localPosition = hits[findqige].transform.localPosition;
                                qiziother.GObj.transform.localPosition = qiziObj_oldlocation;
                                QiziGuanLi.Instance.changxia[(int)qiziObj_oldlocation.x + 4] = qiziother.Index;
                                QiziGuanLi.Instance.QiziCSList.Remove(qiziother);
                                QiziGuanLi.Instance.QiziCSList.Add(qizi);
                            }
                            else //放的位置是场下
                            {
                                QiziGuanLi.Instance.changxia[(int)qiziObj_oldlocation.x + 4] = qiziother.Index;
                                QiziGuanLi.Instance.changxia[(int)qiziother.GObj.transform.localPosition.x + 4] = qizi.Index;
                                qiziobj.transform.localPosition = hits[findqige].transform.localPosition;
                                qiziother.GObj.transform.localPosition = qiziObj_oldlocation;
                            }
                        }
                        else//如果棋子是场上的
                        {   //如果放的位置是场下
                            if (hits[findqige].transform.localPosition.z == -4.5)
                            {
                                QiziGuanLi.Instance.changxia[(int)hits[findqige].transform.localPosition.x + 4] = qizi.Index;
                                QiziGuanLi.Instance.QiziCSList.Remove(qizi);
                                QiziGuanLi.Instance.QiziCSList.Add(qiziother);
                            }//放的位置是场上的棋子，相当于交换两棋子位置
                            qiziother.GObj.transform.position = qiziObj_oldlocation;
                            qiziobj.transform.localPosition = hits[findqige].transform.localPosition;
                        }
                    }
                }
                else
                {
                    qiziobj.transform.localPosition = qiziObj_oldlocation;
                }

                //调用协程延缓1帧去取消出售button
                StartCoroutine(waitforonezhen());
            }
        }
        base.OnUpdate(elapseSeconds, realElapseSeconds);
    }
    IEnumerator waitforonezhen()
    {
        qiziobj = null;
        GetOrNotGetQizi = false;
        yield return new WaitForSeconds(0.12f);
        _btnChushou.gameObject.SetActive(false);
        yield return null;
    }
    private int shengji(int index)
    {
        int num = 0;
        int qizi1=-1;
        int qizi2=-1;
        int qizi3 = -1;
        for (int i=0;i<QiziGuanLi.Instance.QiziList.Count;i++)
        {
            if (QiziGuanLi.Instance.QiziList[i].Index== index&& QiziGuanLi.Instance.QiziList[i].level==1)
            {
                num++;
                if (num == 1)
                {
                    qizi1 = i;
                }
                else if (num == 2)
                {
                    qizi2 = i;
                }
                else
                {
                    qizi3 = i;
                }
            }
        }
        if (num == 2)//场上有两个一星棋子
        {
            EntityQizi qz1 = QiziGuanLi.Instance.QiziList[qizi1];
            EntityQizi qz2 = QiziGuanLi.Instance.QiziList[qizi2];
            qz1.GObj.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            qz1.level = 2;
            if (qz1.money == 1)
            {
                qz1.money = qz1.money * 3;
            }
            else
            {
                qz1.money = qz1.money * 3-1;
            }
            //另一个棋子得放回池子
            if (qz2.GObj.transform.localPosition.z == -4.5f)//如果第二个棋子在场下
            {
                QiziGuanLi.Instance.changxia[(int)qz2.GObj.transform.localPosition.x + 4] = -1;
            }
            else
            {
                QiziGuanLi.Instance.QiziCSList.Remove(qz2);
            }
            QiziGuanLi.Instance.QiziList.Remove(qz2);
            Pool.instance.PoolObject[index].Release(qz2.GObj);
            Pool.instance.PoolEntity.Release(qz2);
            //判断是否存在三个两星棋子
            num = 0;
            for (int i = 0; i < QiziGuanLi.Instance.QiziList.Count; i++)
            {
                if (QiziGuanLi.Instance.QiziList[i].Index == index && QiziGuanLi.Instance.QiziList[i].level == 2)
                {
                    num++;
                    if (num == 1)
                    {
                        qizi1 = i;
                    }
                    else if (num == 2)
                    {
                        qizi2 = i;
                    }
                    else
                    {
                        qizi3 = i;
                    }
                }
            }
            if (num==3)//有三个两星棋子
            {
                qz1 = QiziGuanLi.Instance.QiziList[qizi1];
                qz2 = QiziGuanLi.Instance.QiziList[qizi2];
                EntityQizi qz3 = QiziGuanLi.Instance.QiziList[qizi3];
                qz1.GObj.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                qz1.level = 3;
                if (qz1.money == 3)
                {
                    qz1.money = qz1.money * 3;
                }
                else
                {
                    qz1.money = qz1.money * 3 + 2;
                }
                //另外俩个棋子得放回池子
                if (qz2.GObj.transform.localPosition.z == -4.5f)//如果第二个棋子在场下
                {
                    QiziGuanLi.Instance.changxia[(int)qz2.GObj.transform.localPosition.x + 4] = -1;
                }
                else
                {
                    QiziGuanLi.Instance.QiziCSList.Remove(qz2);
                }
                QiziGuanLi.Instance.QiziList.Remove(qz2);
                Pool.instance.PoolObject[index].Release(qz2.GObj);
                Pool.instance.PoolEntity.Release(qz2);
                if (qz3.GObj.transform.localPosition.z == -4.5f)//如果第三个棋子在场下
                {
                    QiziGuanLi.Instance.changxia[(int)qz3.GObj.transform.localPosition.x + 4] = -1;
                }
                else
                {
                    QiziGuanLi.Instance.QiziCSList.Remove(qz3);
                }
                QiziGuanLi.Instance.QiziList.Remove(qz3);
                Pool.instance.PoolObject[index].Release(qz3.GObj);
                Pool.instance.PoolEntity.Release(qz3);
            }
            return 0;//升级了棋子返回0
        }
        else
        {
            return -1;//没升级返回-1
        }
    }
}
