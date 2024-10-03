using System;
using System.Collections;
using System.Collections.Generic;
using Entity;
using Procedure;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using SelfEventArg;

public class BattleMainCtrl : UIFormLogic
{
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
    
    //拖拽棋子相关
    private bool GetOrNotGetQizi = false;
    EntityQizi qizi;
    EntityQizi qiziother;
    private Plane _plane;
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _plane = new Plane(Vector3.up, Vector3.zero);
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (Input.GetMouseButtonDown(1))//显示棋子属性面板
        {
            qizi = null;
            var targetQizi = GetMousePosQizi();
            if (targetQizi!=null)
            {
                shuxin_qizi = targetQizi;
                qizishuxin.gameObject.SetActive(true);
                shuxinxianshi(shuxin_qizi);
            }
        }

        if (GameEntry.HeroManager.dangqianliucheng == 0)
        {
            if (!GetOrNotGetQizi)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    qizi = null;
                    qizishuxin.gameObject.SetActive(false);
                    var targetQizi = GetMousePosQizi(false);
                    if (targetQizi != null)
                    {
                        qizi = targetQizi;
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
                    qizi.LogicPosition = hitPoint + Vector3.up*0.5f;
                
                }
                qizi.HpBar.UpdataMoveHpBar();
                if (Input.GetMouseButtonUp(0))//抬起鼠标并且当前有拉起己方棋子，放下棋子
                {
                    GetOrNotGetQizi = false;
                    //获取当前鼠标是否在一个格子内
                    if (GetMousePosGezi(out var geziPos, false))
                    {
                        //另一个格子里有hero
                        if (GameEntry.HeroManager.GetQiziByQigeIndex(geziPos, out var curPosQizi))
                        {
                            GameEntry.HeroManager.UpdateEntityPos(curPosQizi,new Vector2Int(qizi.columnIndex,qizi.rowIndex));
                        }
                        GameEntry.HeroManager.UpdateEntityPos(qizi,geziPos);
                    }
                    else
                    {
                        //不在格子中释放qizi
                        qizi.LogicPosition = GameEntry.HeroManager.GetGeziPos(qizi.rowIndex,qizi.columnIndex);
                    }
                    qizi.HpBar.UpdataMoveHpBar();
                    qizi = null;
                }
                
            }
        }
        base.OnUpdate(elapseSeconds, realElapseSeconds);
    }

    private EntityQizi GetMousePosQizi(bool containEnemy = true)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        EntityQizi target;
        if (_plane.Raycast(ray, out var enter))
        {
            // 计算相交点
            var hitPoint = ray.GetPoint(enter);
            for (int i = 0; i < GameEntry.HeroManager.QiziCSList.Count; i++)
            {
                if (Vector3.Distance(GameEntry.HeroManager.QiziCSList[i].LogicPosition,hitPoint)<0.5f)
                {
                    return GameEntry.HeroManager.QiziCSList[i];
                }
            }

            if (containEnemy)
            {
                for (int i = 0; i < GameEntry.HeroManager.DirenList.Count; i++)
                {
                    if (Vector3.Distance(GameEntry.HeroManager.DirenList[i].LogicPosition,hitPoint)<0.5f)
                    {
                        return GameEntry.HeroManager.DirenList[i];
                    }
                }
            }
        }
        return null;
    }

    private bool GetMousePosGezi(out Vector2Int geziPos,bool containEnemy = true)
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
        // xuetiaonow.text = qz.xueliangnow.ToString();
        // xuetiaosum.text = qz.xueliangsum.ToString();
        // pownow.text = qz.powernow.ToString();
        // powsum.text = qz.powersum.ToString();
        // _slderXuetiao.value = qz.xueliangnow / qz.xueliangsum;
        // _slderPow.value = qz.powernow / qz.powersum;
        
        //levelqizi.sprite = GameEntry.HeroManager.ListQiziLevelSprite[qz.level - 1];
        //qiziImage.sprite = ListQiziShuxingSprite[qz.HeroID];
    }
}
