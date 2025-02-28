using System;
using System.Collections;
using System.Collections.Generic;
using Entity;
using Procedure;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using SelfEventArg;
using SkillSystem;
using TMPro;
using UnityEngine.Pool;

public class BattleMainCtrl : UIFormLogic
{
    #region ----------------棋子属性面板------------------------

    [SerializeField] private Image qizishuxin; //显示棋子属性面板
    private EntityQizi shuxin_qizi;
    public TextMeshProUGUI heroName; //角色名称
    [SerializeField] private TextMeshProUGUI xuetiaonow; //显示血量
    [SerializeField] private TextMeshProUGUI xuetiaosum;
    [SerializeField] public Slider _slderXuetiao; //显示血条
    [SerializeField] public Slider _slderHudun; //显示护盾
    [SerializeField] private TextMeshProUGUI pownow; //显示蓝量
    [SerializeField] private TextMeshProUGUI powsum;
    [SerializeField] public Slider _slderPow; //显示蓝条
    [SerializeField] public Image qiziImage; //显示棋子image
    public BattleBagItem itemTemp;
    [SerializeField] private Transform _equipItemParent;
    [SerializeField] private Transform _releaseItemPa;
    public TextMeshProUGUI AttrAttackNum;
    public TextMeshProUGUI AttrHudunAddNum;
    public TextMeshProUGUI AttrAtkSpeed;
    public TextMeshProUGUI AttrCDNum;
    private ObjectPool<BattleBagItem> _itemPool;
    private List<BattleBagItem> _curHeroEquipItemList = new();

    #endregion

    //拖拽棋子相关
    private bool GetOrNotGetQizi = false;
    EntityQizi tryDragTargetQizi;
    EntityQizi curTargetQizi;
    EntityQizi qiziother;
    private Plane _plane;

    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _plane = new Plane(Vector3.up, Vector3.zero);
        _itemPool ??= new ObjectPool<BattleBagItem>(() =>
            {
                GameObject ob = Instantiate(itemTemp.gameObject, _releaseItemPa);
                BattleBagItem ri = ob.GetComponent<BattleBagItem>();
                if (ri != null)
                {
                    ri.OnClickPointCallback = null;
                    ri.OnPointEnterCallback = OnPointItemEnter;
                    ri.OnPointExitCallback = OnPointItemExit;
                    ri.Init();
                }

                return ri;
            }, (item) => { item.gameObject.SetActive(false); }, (item) =>
            {
                item.gameObject.SetActive(false);
                item.OnRelease();
                item.transform.SetParent(_releaseItemPa);
            },
            (item) =>
            {
                Destroy(item.gameObject);
                Destroy(item);
            });
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        qizishuxin.gameObject.SetActive(false);
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (Input.GetMouseButtonUp(1) && tryDragTargetQizi == null)
        {
            curTargetQizi = GetMousePosQizi();
            if (curTargetQizi != null)
            {
                qizishuxin.gameObject.SetActive(true);
                shuxinxianshi(curTargetQizi);
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            tryDragTargetQizi = null;
            curTargetQizi = null;
            var targetQizi = GetMousePosQizi();
            if (targetQizi != null)
            {
                if (targetQizi.BelongCamp == CampType.Friend && GameEntry.HeroManager.dangqianliucheng == 0)
                {
                    tryDragTargetQizi = targetQizi;
                }

                curTargetQizi = targetQizi;
                GetOrNotGetQizi = true;
            }
        }

        if (tryDragTargetQizi != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            // 检查射线是否与平面相交
            if (_plane.Raycast(ray, out var enter))
            {
                // 计算相交点
                var hitPoint = ray.GetPoint(enter);
                tryDragTargetQizi.LogicPosition = hitPoint + Vector3.up * 0.5f;
            }

            tryDragTargetQizi.HpBar.UpdataMoveHpBar();
        }

        if (Input.GetMouseButtonUp(0)) //抬起鼠标并且当前有拉起己方棋子，放下棋子
        {
            if (tryDragTargetQizi != null)
            {
                GetOrNotGetQizi = false;
                bool keepCurTarget = false;
                //获取当前鼠标是否在一个格子内
                if (GetMousePosGezi(out var geziPos, false))
                {
                    //另一个格子里有hero
                    if (GameEntry.HeroManager.GetQiziByQigeIndex(geziPos, out var curPosQizi))
                    {
                        GameEntry.HeroManager.UpdateEntityPos(curPosQizi, new Vector2Int(tryDragTargetQizi.columnIndex, tryDragTargetQizi.rowIndex));
                    }
                    if (geziPos.y == tryDragTargetQizi.rowIndex && geziPos.x == tryDragTargetQizi.columnIndex) //選中位置沒有移動
                    {
                        qizishuxin.gameObject.SetActive(true);
                        shuxinxianshi(tryDragTargetQizi);
                        keepCurTarget = true;
                    }

                    GameEntry.HeroManager.UpdateEntityPos(tryDragTargetQizi, geziPos);
                }
                else
                {
                    //不在格子中释放qizi
                    tryDragTargetQizi.LogicPosition = GameEntry.HeroManager.GetGeziPos(tryDragTargetQizi.rowIndex, tryDragTargetQizi.columnIndex);
                }

                tryDragTargetQizi.HpBar.UpdataMoveHpBar();
                tryDragTargetQizi = null;
                if (keepCurTarget == false)
                {
                    curTargetQizi = null;
                    qizishuxin.gameObject.SetActive(false);
                }
            }
            else if (curTargetQizi != null)
            {
                qizishuxin.gameObject.SetActive(true);
                shuxinxianshi(curTargetQizi);
            }
            else if (curTargetQizi == null)
            {
                qizishuxin.gameObject.SetActive(false);
            }
        }

        /*if (GameEntry.HeroManager.dangqianliucheng == 0)
        {
            if (!GetOrNotGetQizi)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    curTargetQizi = null;
                    qizishuxin.gameObject.SetActive(false);
                    var targetQizi = GetMousePosQizi(false);
                    if (targetQizi != null)
                    {
                        curTargetQizi = targetQizi;
                        GetOrNotGetQizi = true;
                    }
                }
            }
            else
            {
                //拉起棋子跟随鼠标移动
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                // 检查射线是否与平面相交
                if (_plane.Raycast(ray, out var enter))
                {
                    // 计算相交点
                    var hitPoint = ray.GetPoint(enter);
                    curTargetQizi.LogicPosition = hitPoint + Vector3.up*0.5f;

                }
                curTargetQizi.HpBar.UpdataMoveHpBar();
                if (Input.GetMouseButtonUp(0))//抬起鼠标并且当前有拉起己方棋子，放下棋子
                {
                    GetOrNotGetQizi = false;
                    //获取当前鼠标是否在一个格子内
                    if (GetMousePosGezi(out var geziPos, false))
                    {
                        //另一个格子里有hero
                        if (GameEntry.HeroManager.GetQiziByQigeIndex(geziPos, out var curPosQizi))
                        {
                            GameEntry.HeroManager.UpdateEntityPos(curPosQizi,new Vector2Int(curTargetQizi.columnIndex,curTargetQizi.rowIndex));
                        }
                        GameEntry.HeroManager.UpdateEntityPos(curTargetQizi,geziPos);
                    }
                    else
                    {
                        //不在格子中释放qizi
                        curTargetQizi.LogicPosition = GameEntry.HeroManager.GetGeziPos(curTargetQizi.rowIndex,curTargetQizi.columnIndex);
                    }
                    curTargetQizi.HpBar.UpdataMoveHpBar();
                    curTargetQizi = null;
                }

            }
        }*/
        if (curTargetQizi != null && curTargetQizi.IsValid&&qizishuxin.gameObject.activeSelf)
        {
            shuxinxianshi(curTargetQizi);
        }
        base.OnUpdate(elapseSeconds, realElapseSeconds);
    }

    private EntityQizi GetMousePosQizi(bool containEnemy = true)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        EntityQizi target;
        _plane.SetNormalAndPosition(Vector3.up, GameEntry.HeroManager.QigePosOffset);
        if (_plane.Raycast(ray, out var enter))
        {
            // 计算相交点
            var hitPoint = ray.GetPoint(enter);
            for (int i = 0; i < GameEntry.HeroManager.QiziCSList.Count; i++)
            {
                hitPoint.y = GameEntry.HeroManager.QiziCSList[i].LogicPosition.y; //放置y分量影响
                if (Vector3.Distance(GameEntry.HeroManager.QiziCSList[i].LogicPosition, hitPoint) < 0.5f)
                {
                    return GameEntry.HeroManager.QiziCSList[i];
                }
            }

            if (containEnemy)
            {
                for (int i = 0; i < GameEntry.HeroManager.DirenList.Count; i++)
                {
                    if (Vector3.Distance(GameEntry.HeroManager.DirenList[i].LogicPosition, hitPoint) < 0.5f)
                    {
                        return GameEntry.HeroManager.DirenList[i];
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="geziPos">column，row</param>
    /// <param name="containEnemy"></param>
    /// <returns></returns>
    private bool GetMousePosGezi(out Vector2Int geziPos, bool containEnemy = true)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        // 检查射线是否与平面相交
        if (_plane.Raycast(ray, out var enter))
        {
            // 计算相交点
            var hitPoint = ray.GetPoint(enter);
            if (GameEntry.HeroManager.CheckInGezi(hitPoint, out geziPos))
            {
                return containEnemy || geziPos.y <= 3;
            }
        }

        geziPos = Vector2Int.zero;
        return false;
    }

    private void shuxinxianshi(EntityQizi qz)
    {
        heroName.text = qz.HeroUID.ToString();
        var maxHp = (int)qz.GetAttribute(AttributeType.MaxHp).GetFinalValue();
        var curHp = (int)qz.GetAttribute(AttributeType.Hp).GetFinalValue();
        var curHuDun = (int)qz.GetAttribute(AttributeType.HuDun).GetFinalValue();
        var maxPower = (int)qz.GetAttribute(AttributeType.MaxPower).GetFinalValue();
        var curPower = (int)qz.GetAttribute(AttributeType.Power).GetFinalValue();
        if (curHp + curHuDun > maxHp)
        {
            float curSum = curHp + curHuDun;
            _slderXuetiao.value = curHp / curSum;
            _slderHudun.value = curHuDun / curSum;
        }
        else
        {
            _slderXuetiao.value = curHp / (float)maxHp;
            _slderHudun.value = curHuDun / (float)maxHp;
        }

        xuetiaonow.text = (curHp + curHuDun).ToString();
        xuetiaosum.text = maxHp.ToString();
        pownow.text = curPower.ToString();
        powsum.text = maxPower.ToString();
        _slderPow.value = curPower / (float)maxPower;
        AttrAttackNum.text = ((int)qz.GetAttribute(AttributeType.AttackDamage).GetFinalValue()).ToString();
        AttrAtkSpeed.text = ((int)qz.GetAttribute(AttributeType.AttackSpeed).GetFinalValue()).ToString();
        AttrHudunAddNum.text = ((int)qz.GetAttribute(AttributeType.HuDunBoost).GetFinalValue()).ToString();
        AttrCDNum.text = ((int)qz.GetAttribute(AttributeType.CooldownReduce).GetFinalValue()).ToString();
        ShowHeroEquip();
    }

    private void ShowHeroEquip()
    {
        if (HaveEquipChange())
        {
            ClearCurHeroEquipItem();
            if (curTargetQizi != null)
            {
                foreach (var itemID in curTargetQizi.EquipItemList)
                {
                    var oneItem = _itemPool.Get();
                    oneItem.transform.SetParent(_equipItemParent);
                    oneItem.ItemID = itemID;
                    oneItem.itemNum = 1;
                    oneItem.CurItemType = BattleBagItem.ItemType.BattleDetailHeroEquip;
                    oneItem.Fresh();
                    _curHeroEquipItemList.Add(oneItem);
                }
            }
        }
        else
        {
            //刷新cd
        }
    }

    private bool HaveEquipChange()
    {
        if (_curHeroEquipItemList != null && curTargetQizi != null && curTargetQizi.EquipItemList != null)
        {
            if (_curHeroEquipItemList.Count == curTargetQizi.EquipItemList.Count)
            {
                for (int equipIndex = 0; equipIndex < _curHeroEquipItemList.Count; equipIndex++)
                {
                    var item = _curHeroEquipItemList[equipIndex];
                    var heroEquip = curTargetQizi.EquipItemList[equipIndex];
                    if (item.ItemID != heroEquip)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        return true;
    }
    private void ClearCurHeroEquipItem()
    {
        foreach (var item in _curHeroEquipItemList)
        {
            _itemPool?.Release(item);
        }

        _curHeroEquipItemList.Clear();
    }

    private void OnPointItemEnter(BattleBagItem battleBagItem)
    {
    }

    private void OnPointItemExit(BattleBagItem battleBagItem)
    {
    }
}