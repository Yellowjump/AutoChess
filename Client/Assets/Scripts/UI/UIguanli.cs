using GameFramework.Fsm;
using Entity;
using System;
using System.Collections;
using System.Collections.Generic;
using SkillSystem;
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
    public Text textjindu;//显示流程进度倒计时

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
    private EntityQizi shuxin_qizi;
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
    [SerializeField]
    public Image levelqizi;//显示棋子level
    [SerializeField]
    public Image qiziImage;//显示棋子image

    Jinqian jinqian=new Jinqian();//创建一个金钱类对象
    //拖拽棋子相关
    private bool GetOrNotGetQizi = false;
    EntityQizi qizi;
    EntityQizi qiziother;
    //Vector3 qiziObj_oldlocation;

    private List<Sprite>ListQiziSprite = new List<Sprite>();//保存棋子图片
    private List<Sprite> ListQiziShuxingSprite = new List<Sprite>();//保存棋子图片
    public override void OnInit(object userData)
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
        InitQiziShuxingPicture();//保存棋子属性界面图片，使用的时候需注意ListQiziShuxingPicture[qiziindex];
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
    private void InitQiziShuxingPicture()
    {
        Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Image/shuxing0.jpg");
        ListQiziShuxingSprite.Add(spr);
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
            if (QiziGuanLi.Instance.goumaiUIqiziID[0] > 0)
            {
                _btnOne.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziID[0]-1];
            }
            if (QiziGuanLi.Instance.goumaiUIqiziID[1] > 0)
            {
                _btnTwo.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziID[1]-1];
            }
            if (QiziGuanLi.Instance.goumaiUIqiziID[2] > 0)
            {
                _btnThree.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziID[2]-1];
            }
            if (QiziGuanLi.Instance.goumaiUIqiziID[3] > 0)
            {
                _btnFour.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziID[3]-1];
            }
            if (QiziGuanLi.Instance.goumaiUIqiziID[4] > 0)
            {
                _btnFive.image.sprite = ListQiziSprite[QiziGuanLi.Instance.goumaiUIqiziID[4]-1];
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
        int index = QiziGuanLi.Instance.goumaiUIqiziID[0];
        int paikuindex = QiziGuanLi.Instance.goumaiUIqiziPaikuIndex[0];
        goumaiqizi(index, paikuindex, _btnOne);
    }
    private void OnClickBtnGouMaiTwo()
    {
        int index = QiziGuanLi.Instance.goumaiUIqiziID[1];
        int paikuindex = QiziGuanLi.Instance.goumaiUIqiziPaikuIndex[1];
        goumaiqizi(index, paikuindex, _btnTwo);

    }
    private void OnClickBtnGouMaiThree()
    {
        int index = QiziGuanLi.Instance.goumaiUIqiziID[2];
        int paikuindex = QiziGuanLi.Instance.goumaiUIqiziPaikuIndex[2];
        goumaiqizi(index, paikuindex, _btnThree);
    }
    private void OnClickBtnGouMaiFour()
    {
        int index = QiziGuanLi.Instance.goumaiUIqiziID[3];
        int paikuindex = QiziGuanLi.Instance.goumaiUIqiziPaikuIndex[3];
        goumaiqizi(index, paikuindex, _btnFour);
    }
    private void OnClickBtnGouMaiFive()
    {
        int index = QiziGuanLi.Instance.goumaiUIqiziID[4];
        int paikuindex = QiziGuanLi.Instance.goumaiUIqiziPaikuIndex[4];
        goumaiqizi(index, paikuindex, _btnFive);
    }
    private void goumaiqizi(int index,int paikuIndex,Button btn)
    {
        int kwCx = QiziGuanLi.Instance.findkongweiCX();
        int feiyong = QiziGuanLi.Instance.qizi[index-1];

        if (jinqian.GetJinBiNum() >= feiyong)//先场下有位置，再买得起
        {
            int SJ = shengji(index);
            if (SJ == -1 && kwCx != -1)//没升级棋子但是有空位
            {
                QiziGuanLi.Instance.goumaiqizi(index, paikuIndex, kwCx, feiyong);
                qizi = Pool.instance.PoolEntity.Get() as EntityQizi;
                qizi.BelongCamp = CampType.Friend;
                qizi.Init(index);
                qizi.rowIndex =-1;
                qizi.columnIndex = kwCx;
                qizi.LogicPosition = QiziGuanLi.instance.GetGeziPos(qizi.rowIndex, qizi.columnIndex);
                qizi = null;
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
        if (qizi.rowIndex != -1)//棋子在场上
        {
            QiziGuanLi.Instance.QiziCSList.Remove(qizi);

        }
        else //棋子在场下
        {
            QiziGuanLi.Instance.changxia[qizi.columnIndex] = -1;
            QiziGuanLi.Instance.QiziCXList.Remove(qizi);
        }
        QiziGuanLi.Instance.QiziList.Remove(qizi);
        int jq = qizi.money;//获取棋子的金额
        jinqian.changejinqian(jq);//卖完棋子之后加钱，并把棋子放回池子里,牌库也要更新
        JinBi.text = jinqian.GetJinBiNum().ToString();
        QiziGuanLi.Instance.chushouQizi(qizi.HeroID, jq,qizi.level);
        Pool.instance.PoolObject[qizi.HeroID].Release(qizi.GObj);
        Pool.instance.PoolEntity.Release(qizi);
        qizi = null;
    }
    private void shuxinxianshi(EntityQizi qz)
    {
        xuetiaonow.text = qz.xueliangnow.ToString();
        xuetiaosum.text = qz.xueliangsum.ToString();
        pownow.text = qz.powernow.ToString();
        powsum.text = qz.powersum.ToString();
        _slderXuetiao.value = qz.xueliangnow / qz.xueliangsum;
        _slderPow.value = qz.powernow / qz.powersum;
        levelqizi.sprite = QiziGuanLi.Instance.ListQiziLevelSprite[qz.level - 1];
        qiziImage.sprite = ListQiziShuxingSprite[qz.HeroID];
    }
    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (!GetOrNotGetQizi)
        {
            if (Input.GetMouseButtonDown(1))//显示棋子属性面板
            {
                qizi = null;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit) && hit.transform.tag == "qizi")
                {
                    for (int i = 0; i < QiziGuanLi.Instance.QiziList.Count; i++)
                    {
                        if (hit.transform.position == QiziGuanLi.Instance.QiziList[i].GObj.transform.position)
                        {
                            shuxin_qizi = QiziGuanLi.Instance.QiziList[i];
                            break;
                        }
                    }
                    for (int i = 0; i < QiziGuanLi.Instance.DirenList.Count; i++)
                    {
                        if (hit.transform.position == QiziGuanLi.Instance.DirenList[i].GObj.transform.position)
                        {
                            shuxin_qizi = QiziGuanLi.Instance.DirenList[i];
                            break;
                        }
                    }
                    //Log.Info("hfk:" + hit.transform.tag);
                    qizishuxin.gameObject.SetActive(true);
                    shuxinxianshi(shuxin_qizi);
                }
                ////测试qigepos
                /*if (Physics.Raycast(ray, out hit) && hit.transform.tag == "qige")
                {

                    Vector2Int v = QiziGuanLi.Instance.getIndexQige(hit.transform.position);
                    Log.Info("hfk" + hit.transform.position + "  vector2:" + v);
                }*/
            }
            if (Input.GetMouseButtonDown(0))
            {
                qizi = null;
                qizishuxin.gameObject.SetActive(false);
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (QiziGuanLi.Instance.dangqianliucheng == 0)
                {
                    if (Physics.Raycast(ray, out hit) && hit.transform.tag == "qizi" && hit.transform.position.z < 0)
                    {
                        for (int i = 0; i < QiziGuanLi.Instance.QiziList.Count; i++)
                        {
                            if (hit.transform.position == QiziGuanLi.Instance.QiziList[i].GObj.transform.position)
                            {
                                qizi = QiziGuanLi.Instance.QiziList[i];
                                break;
                            }
                        }
                        //Log.Info("hfk:" + hit.transform.tag);
                        //qiziObj_oldlocation = qizi.GObj.transform.position;
                        GetOrNotGetQizi = true;
                        _btnChushou.gameObject.SetActive(true);
                        ChuSouNum.text = "出售\n" + qizi.money + "金币";
                    }
                }
                else//战斗阶段，只能选场下的棋子
                {
                    // 创建一个平面，平面法线方向为 Vector3.up，平面通过点 (0, 0, 0)
                    Plane plane = new Plane(Vector3.up, Vector3.zero);
                    // 检查射线是否与平面相交
                    if (plane.Raycast(ray, out var enter))
                    {
                        // 计算相交点
                        Vector3 hitPoint = ray.GetPoint(enter);
                        if (hitPoint.z > -5 && hitPoint.z < -4 && hitPoint.x > -4.5 && hitPoint.x < 4.5)
                        {
                            var matchIndex = (int)(hitPoint.x + 4.5);
                            foreach (var cxQizi in QiziGuanLi.instance.QiziCXList)
                            {
                                if(cxQizi.columnIndex==matchIndex)
                                {
                                    qizi = cxQizi;
                                    GetOrNotGetQizi = true;
                                    _btnChushou.gameObject.SetActive(true);
                                    ChuSouNum.text = "出售\n" + qizi.money + "金币";
                                    break;
                                }
                            }
                        }
                    }
                    /*if (Physics.Raycast(ray, out hit) && hit.transform.tag == "qizi" && hit.transform.position.z == -4.5)
                    {
                        for (int i = 0; i < QiziGuanLi.Instance.QiziCXList.Count; i++)
                        {
                            if (hit.transform.position == QiziGuanLi.Instance.QiziCXList[i].GObj.transform.position)
                            {
                                qizi = QiziGuanLi.Instance.QiziCXList[i];
                                break;
                            }
                        }
                        //Log.Info("hfk:" + hit.transform.tag);
                        //qiziObj_oldlocation = qizi.GObj.transform.position;
                        GetOrNotGetQizi = true;
                        _btnChushou.gameObject.SetActive(true);
                        ChuSouNum.text = "出售\n" + qizi.money + "金币";
                    }*/
                }
            }
        }
        else //if (timenow <= Time.time)
        {
            qiziother = null;
            int findotherQizi = -1;
            int findqige = -1;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            // 创建一个平面，平面法线方向为 Vector3.up，平面通过点 (0, 0, 0)
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            bool planeHasEnter = false;
            Vector3 hitPoint = Vector3.zero;
            // 检查射线是否与平面相交
            if (plane.Raycast(ray, out var enter))
            {
                planeHasEnter = true;
                // 计算相交点
                hitPoint = ray.GetPoint(enter);
                qizi.LogicPosition = hitPoint + Vector3.up*0.5f;
                
            }

            RaycastHit[] hits = Physics.RaycastAll(ray);
            for (int i = 0; i < hits.Length; i++)//移动鼠标拖拽棋子
            {
                if (hits[i].transform.tag == "qige")
                {
                    qizi.GObj.transform.position = hits[i].point + new Vector3(0, 0.2f, 0);
                    findqige = i;
                }
                else if (hits[i].transform.name == "qipan")
                {
                    qizi.GObj.transform.position = hits[i].point + new Vector3(0, 0.5f, 0);
                }
                if (hits[i].transform.tag == "qizi" && hits[i].transform.position != qizi.GObj.transform.position)
                {
                    findotherQizi = i;
                }
            }
            if (Input.GetMouseButtonDown(0))//判断射线检测，应该放下棋子到棋格子上还是和已经放置的棋子交换位置，或者无法放置棋子（回到原位）
            {
                var matchRowIndex = -1;
                var matchColumnIndex = -1;
                var sqrt3 = Math.Sqrt(3);
                if (hitPoint.z > -5 && hitPoint.z < -4 && hitPoint.x > -4.5 && hitPoint.x < 4.5)//确定选中的是场上格子还是场下格子
                {
                    matchRowIndex = -1;
                    matchColumnIndex = (int)(hitPoint.x + 4.5);
                }
                else if (hitPoint.z > -3 - (1 / sqrt3) && hitPoint.z < -3 + (1 / sqrt3) + (sqrt3 * 3.5))
                {
                    matchRowIndex = (int)((hitPoint.z+3+(1/sqrt3))*2/sqrt3);
                    var tempOffset = matchRowIndex % 2 == 0 ? 0.5f : 0;
                    if (hitPoint.x > tempOffset - 4f && hitPoint.x < 3 + tempOffset)
                    {
                        matchColumnIndex = (int)(hitPoint.x + 4f - tempOffset);
                    }
                }

                if (matchColumnIndex != -1)//命中了格子
                {
                    EntityQizi hitQizi = null;
                    if (matchRowIndex == -1)//命中的场下格子
                    {
                        foreach (var cxQizi in QiziGuanLi.instance.QiziCXList)
                        {
                            if(cxQizi.columnIndex==matchColumnIndex)
                            {
                                hitQizi = cxQizi;
                                break;
                            }
                        }
                    }
                    else
                    {
                        foreach (var csQizi in QiziGuanLi.instance.QiziCSList)
                        {
                            if(csQizi.columnIndex==matchColumnIndex)
                            {
                                hitQizi = csQizi;
                                break;
                            }
                        }
                    }

                    if (hitQizi == null&&(matchRowIndex == -1 || (QiziGuanLi.Instance.dangqianliucheng == 0&&(QiziGuanLi.Instance.QiziCSList.Count < Dengji.Instance.getDj()||(QiziGuanLi.Instance.QiziCSList.Count == Dengji.Instance.getDj()&&qizi.rowIndex != -1))))) //对面空格子并且能直接放进去的情况
                    {
                        //如果拖拽的棋子是场下的
                        if (qizi.rowIndex == -1)
                        {   //放的位置是场上
                            if (matchRowIndex!=-1)
                            {   
                                 qizi.LogicPosition = QiziGuanLi.instance.GetGeziPos(matchRowIndex,matchColumnIndex);
                                 QiziGuanLi.Instance.changxia[qizi.columnIndex] = -1;
                                 QiziGuanLi.Instance.QiziCSList.Add(qizi);
                                 QiziGuanLi.Instance.QiziCXList.Remove(qizi);
                                 qizi.rowIndex = matchRowIndex;
                                 qizi.columnIndex= matchColumnIndex;

                            }
                            else //放的位置是场下
                            {
                                qizi.LogicPosition = QiziGuanLi.instance.GetGeziPos(matchRowIndex,matchColumnIndex);
                                QiziGuanLi.Instance.changxia[qizi.columnIndex] = -1;
                                QiziGuanLi.Instance.changxia[matchColumnIndex] = qizi.HeroID;
                                qizi.rowIndex = matchRowIndex;
                                qizi.columnIndex = matchColumnIndex;
                            }
                        }
                        else//如果棋子是场上的
                        {   //如果放的位置是场下
                            if (matchRowIndex == -1)
                            {
                                QiziGuanLi.Instance.changxia[matchColumnIndex] = qizi.HeroID;
                                QiziGuanLi.Instance.QiziCSList.Remove(qizi);
                                QiziGuanLi.Instance.QiziCXList.Add(qizi);
                            }
                            qizi.LogicPosition = QiziGuanLi.instance.GetGeziPos(matchRowIndex,matchColumnIndex);
                            qizi.rowIndex = matchRowIndex;
                            qizi.columnIndex = matchColumnIndex;
                        }
                    }
                    else if (hitQizi != null && QiziGuanLi.Instance.dangqianliucheng == 0 || matchRowIndex == -1) //交换棋子的情况
                    {
                        //如果拖拽的棋子是场下的
                        if (qizi.rowIndex == -1)
                        {   //放的棋子位置是场上
                            if (hitQizi.rowIndex != -1)
                            {
                                QiziGuanLi.Instance.changxia[qizi.columnIndex] = hitQizi.HeroID;
                                QiziGuanLi.Instance.QiziCSList.Remove(hitQizi);
                                QiziGuanLi.Instance.QiziCSList.Add(qizi);
                                QiziGuanLi.Instance.QiziCXList.Remove(qizi);
                                QiziGuanLi.Instance.QiziCXList.Add(hitQizi);
                            }
                            else //放的位置是场下
                            {
                                QiziGuanLi.Instance.changxia[hitQizi.columnIndex] = qizi.HeroID;
                                QiziGuanLi.Instance.changxia[qizi.columnIndex] = hitQizi.HeroID;
                            }
                        }
                        else//如果棋子是场上的
                        {   //如果放的别的棋子位置是场下
                            if (hitQizi.rowIndex == -1)
                            {
                                QiziGuanLi.Instance.changxia[hitQizi.columnIndex] = qizi.HeroID;
                                QiziGuanLi.Instance.QiziCSList.Remove(qizi);
                                QiziGuanLi.Instance.QiziCSList.Add(qiziother);
                                QiziGuanLi.Instance.QiziCXList.Remove(qiziother);
                                QiziGuanLi.Instance.QiziCXList.Add(qizi);
                            }//放的位置是场上的棋子，相当于交换两棋子位置
                        }
                        (qizi.LogicPosition,hitQizi.LogicPosition)=(QiziGuanLi.instance.GetGeziPos(hitQizi.rowIndex,hitQizi.columnIndex),QiziGuanLi.instance.GetGeziPos(qizi.rowIndex,qizi.columnIndex));
                        (qizi.columnIndex, hitQizi.columnIndex) = (hitQizi.columnIndex, qizi.columnIndex);
                        (qizi.rowIndex, hitQizi.rowIndex) = (hitQizi.rowIndex, qizi.rowIndex);
                        
                    }
                    else//将qizi放回原位
                    {
                        qizi.LogicPosition = QiziGuanLi.instance.GetGeziPos(qizi.rowIndex,qizi.columnIndex);
                    }
                    qizi = null;
                }
                else
                {
                    qizi.LogicPosition = QiziGuanLi.instance.GetGeziPos(qizi.rowIndex,qizi.columnIndex);
                    qizi = null;
                }
                /*if (findqige != -1 && hits[findqige].transform.position.z<0)//鼠标不在棋格上，放回原位
                {
                    if (QiziGuanLi.Instance.dangqianliucheng == 0)
                    {
                        if (findotherQizi == -1)//空格子
                        {
                            //如果拖拽的棋子是场下的
                            if (qizi.rowIndex == -1)
                            {   //放的位置是场上
                                if (hits[findqige].transform.position.z != -4.5)
                                {   //并且场上棋子数小于等于等级，需要将changxia[]所处位置置为-1
                                    if (QiziGuanLi.Instance.QiziCSList.Count < Dengji.Instance.getDj())
                                    {
                                        qizi.GObj.transform.position = hits[findqige].transform.parent.transform.position;
                                        QiziGuanLi.Instance.changxia[qizi.columnIndex] = -1;
                                        QiziGuanLi.Instance.QiziCSList.Add(qizi);
                                        QiziGuanLi.Instance.QiziCXList.Remove(qizi);
                                        qizi.rowIndex = qizi.GObj.transform.position.x;
                                        qizi.columnIndex= qizi.GObj.transform.position.z;
                                    }
                                    else
                                    {
                                        qizi.GObj.transform.position = new Vector3(qizi.rowIndex,0,qizi.columnIndex);
                                    }
                                }
                                else //放的位置是场下
                                {
                                    qizi.GObj.transform.position = hits[findqige].transform.parent.transform.position;
                                    QiziGuanLi.Instance.changxia[(int)qizi.rowIndex + 4] = -1;
                                    QiziGuanLi.Instance.changxia[(int)hits[findqige].transform.parent.transform.position.x + 4] = qizi.Index;
                                    qizi.rowIndex = qizi.GObj.transform.position.x;
                                    qizi.columnIndex = qizi.GObj.transform.position.z;
                                }
                            }
                            else//如果棋子是场上的
                            {   //如果放的位置是场下
                                if (hits[findqige].transform.position.z == -4.5)
                                {
                                    QiziGuanLi.Instance.changxia[(int)hits[findqige].transform.position.x + 4] = qizi.Index;
                                    QiziGuanLi.Instance.QiziCSList.Remove(qizi);
                                    QiziGuanLi.Instance.QiziCXList.Add(qizi);
                                }
                                qizi.GObj.transform.position = hits[findqige].transform.parent.transform.position;
                                qizi.rowIndex = qizi.GObj.transform.position.x;
                                qizi.columnIndex = qizi.GObj.transform.position.z;
                            }
                            qizi = null;
                        }
                        else//棋格上有别的棋子
                        {
                            for (int i = 0; i < QiziGuanLi.Instance.QiziList.Count; i++)
                            {
                                if (hits[findotherQizi].transform.position == QiziGuanLi.Instance.QiziList[i].GObj.transform.position)
                                {
                                    qiziother = QiziGuanLi.Instance.QiziList[i];
                                }
                            }
                            //如果拖拽的棋子是场下的
                            if (qizi.columnIndex == -4.5)
                            {   //放的棋子位置是场上
                                if (hits[findqige].transform.position.z != -4.5)
                                {
                                    qizi.GObj.transform.position = hits[findqige].transform.parent.transform.position;
                                    qiziother.GObj.transform.position = new Vector3(qizi.rowIndex,0,qizi.columnIndex);
                                    QiziGuanLi.Instance.changxia[(int)qizi.rowIndex + 4] = qiziother.Index;
                                    QiziGuanLi.Instance.QiziCSList.Remove(qiziother);
                                    QiziGuanLi.Instance.QiziCSList.Add(qizi);
                                    QiziGuanLi.Instance.QiziCXList.Remove(qizi);
                                    QiziGuanLi.Instance.QiziCXList.Add(qiziother);
                                }
                                else //放的位置是场下
                                {
                                    QiziGuanLi.Instance.changxia[(int)qizi.rowIndex + 4] = qiziother.Index;
                                    QiziGuanLi.Instance.changxia[(int)qiziother.rowIndex + 4] = qizi.Index;
                                    qizi.GObj.transform.position = hits[findqige].transform.parent.transform.position;
                                    qiziother.GObj.transform.position = new Vector3(qizi.rowIndex,0,qizi.columnIndex);
                                }
                            }
                            else//如果棋子是场上的
                            {   //如果放的别的棋子位置是场下
                                if (hits[findqige].transform.position.z == -4.5)
                                {
                                    QiziGuanLi.Instance.changxia[(int)hits[findqige].transform.position.x + 4] = qizi.Index;
                                    QiziGuanLi.Instance.QiziCSList.Remove(qizi);
                                    QiziGuanLi.Instance.QiziCSList.Add(qiziother);
                                    QiziGuanLi.Instance.QiziCXList.Remove(qiziother);
                                    QiziGuanLi.Instance.QiziCXList.Add(qizi);
                                }//放的位置是场上的棋子，相当于交换两棋子位置
                                qiziother.GObj.transform.position = new Vector3(qizi.rowIndex,0,qizi.columnIndex);
                                qizi.GObj.transform.position = hits[findqige].transform.parent.transform.position;
                            }
                            qizi.rowIndex = qizi.GObj.transform.position.x;
                            qizi.columnIndex = qizi.GObj.transform.position.z;
                            qiziother.rowIndex = qiziother.GObj.transform.position.x;
                            qiziother.columnIndex = qiziother.GObj.transform.position.z;
                            qizi = null;
                        }
                    }
                    else//战斗状态下
                    {
                        if (findotherQizi == -1)//空格子
                        {
                            if (hits[findqige].transform.position.z != -4.5)//放的位置是场上，直接归位
                            {
                                qizi.GObj.transform.position = new Vector3(qizi.rowIndex,0,qizi.columnIndex);
                            }
                            else //放的位置是场下
                            {
                                qizi.GObj.transform.position = hits[findqige].transform.parent.transform.position;
                                QiziGuanLi.Instance.changxia[(int)qizi.rowIndex + 4] = -1;
                                QiziGuanLi.Instance.changxia[(int)hits[findqige].transform.position.x + 4] = qizi.Index;
                            }
                            qizi.rowIndex = qizi.GObj.transform.position.x;
                            qizi.columnIndex = qizi.GObj.transform.position.z;
                        }
                        else//棋格上有别的棋子
                        {
                            if (hits[findotherQizi].transform.position.z != -4.5f)//其他棋子不在场下
                            {
                                qizi.GObj.transform.position = new Vector3(qizi.rowIndex, 0, qizi.columnIndex);
                            }
                            else
                            {
                                for (int i = 0; i < QiziGuanLi.Instance.QiziCXList.Count; i++)
                                {
                                    if (hits[findotherQizi].transform.position == QiziGuanLi.Instance.QiziCXList[i].GObj.transform.position)
                                    {
                                        qiziother = QiziGuanLi.Instance.QiziCXList[i];
                                    }
                                }
                                QiziGuanLi.Instance.changxia[(int)qizi.rowIndex + 4] = qiziother.Index;
                                QiziGuanLi.Instance.changxia[(int)qiziother.GObj.transform.position.x + 4] = qizi.Index;
                                qizi.GObj.transform.position = qiziother.GObj.transform.position;
                                qiziother.GObj.transform.position = new Vector3(qizi.rowIndex, 0, qizi.columnIndex);
                                qizi.rowIndex = qizi.GObj.transform.position.x;
                                qizi.columnIndex = qizi.GObj.transform.position.z;
                                qiziother.rowIndex = qiziother.GObj.transform.position.x;
                                qiziother.columnIndex = qiziother.GObj.transform.position.z;
                            }
                        }
                        qizi = null;
                    }
                }
                else
                {
                    qizi.LogicPosition = QiziGuanLi.instance.GetGeziPos(qizi.rowIndex,qizi.columnIndex);
                }*/
                //调用协程延缓1帧去取消出售button
                StartCoroutine(waitforonezhen());
            }
        }
        base.OnUpdate(elapseSeconds, realElapseSeconds);
    }
    IEnumerator waitforonezhen()
    {
        GetOrNotGetQizi = false;
        yield return new WaitForSeconds(0.1f);
        _btnChushou.gameObject.SetActive(false);
        //qizi = null;
        yield return null;
    }
    private int shengji(int index)
    {
        int num = 0;
        int qizi1=-1;
        int qizi2=-1;
        int qizi3 = -1;
        if (QiziGuanLi.Instance.dangqianliucheng == 0)//准备阶段
        {
            for (int i = 0; i < QiziGuanLi.Instance.QiziList.Count; i++)
            {
                if (QiziGuanLi.Instance.QiziList[i].HeroID == index && QiziGuanLi.Instance.QiziList[i].level == 1)
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
                qz1.levelImage.sprite = QiziGuanLi.Instance.ListQiziLevelSprite[1];
                if (qz1.money == 1)
                {
                    qz1.money = qz1.money * 3;
                }
                else
                {
                    qz1.money = qz1.money * 3 - 1;
                }
                //另一个棋子得放回池子
                qz2.Remove();
                //判断是否存在三个两星棋子
                num = 0;
                for (int i = 0; i < QiziGuanLi.Instance.QiziList.Count; i++)
                {
                    if (QiziGuanLi.Instance.QiziList[i].HeroID == index && QiziGuanLi.Instance.QiziList[i].level == 2)
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
                if (num == 3)//有三个两星棋子
                {
                    qz1 = QiziGuanLi.Instance.QiziList[qizi1];
                    qz2 = QiziGuanLi.Instance.QiziList[qizi2];
                    EntityQizi qz3 = QiziGuanLi.Instance.QiziList[qizi3];
                    qz1.GObj.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                    qz1.level = 3;
                    qz1.levelImage.sprite = QiziGuanLi.Instance.ListQiziLevelSprite[2];
                    if (qz1.money == 3)
                    {
                        qz1.money = qz1.money * 3;
                    }
                    else
                    {
                        qz1.money = qz1.money * 3 + 2;
                    }
                    //另外俩个棋子得放回池子
                    qz2.Remove();
                    qz3.Remove();
                }
                return 0;//升级了棋子返回0
            }
            else
            {
                return -1;//没升级返回-1
            }
        }
        else //战斗阶段
        {
            for (int i = 0; i < QiziGuanLi.Instance.QiziCXList.Count; i++)
            {
                if (QiziGuanLi.Instance.QiziCXList[i].HeroID == index && QiziGuanLi.Instance.QiziCXList[i].level == 1)
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
            if (num == 2)//场下有两个一星棋子
            {
                EntityQizi qz1 = QiziGuanLi.Instance.QiziCXList[qizi1];
                EntityQizi qz2 = QiziGuanLi.Instance.QiziCXList[qizi2];
                qz1.GObj.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                qz1.level = 2;
                qz1.levelImage.sprite = QiziGuanLi.Instance.ListQiziLevelSprite[1];
                if (qz1.money == 1)
                {
                    qz1.money = qz1.money * 3;
                }
                else
                {
                    qz1.money = qz1.money * 3 - 1;
                }
                //另一个棋子得放回池子
                qz2.Remove();
                //判断是否存在三个两星棋子
                num = 0;
                for (int i = 0; i < QiziGuanLi.Instance.QiziCXList.Count; i++)
                {
                    if (QiziGuanLi.Instance.QiziCXList[i].HeroID == index && QiziGuanLi.Instance.QiziCXList[i].level == 2)
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
                if (num == 3)//有三个两星棋子
                {
                    qz1 = QiziGuanLi.Instance.QiziList[qizi1];
                    qz2 = QiziGuanLi.Instance.QiziList[qizi2];
                    EntityQizi qz3 = QiziGuanLi.Instance.QiziList[qizi3];
                    qz1.GObj.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                    qz1.level = 3;
                    qz1.levelImage.sprite = QiziGuanLi.Instance.ListQiziLevelSprite[2];
                    if (qz1.money == 3)
                    {
                        qz1.money = qz1.money * 3;
                    }
                    else
                    {
                        qz1.money = qz1.money * 3 + 2;
                    }
                    //另外俩个棋子得放回池子
                    qz2.Remove();
                    qz3.Remove();
                }
                return 0;//升级了棋子返回0
            }
            else
            {
                return -1;//没升级返回-1
            }
        }
    }
}
