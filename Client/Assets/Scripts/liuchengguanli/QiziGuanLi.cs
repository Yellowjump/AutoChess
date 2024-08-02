using Entity;
using System;
using System.Collections;
using System.Collections.Generic;
using SelfEventArg;
using SkillSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using static UnityEngine.GraphicsBuffer;

public partial class QiziGuanLi
{
    public static QiziGuanLi instance;//单实例化
    public static QiziGuanLi Instance
    {
        get
        {
            if (instance == null)
                instance = new QiziGuanLi();
            return instance;
        }
    }
    QiziGuanLi()
    {
        Init();//初始化paiku*
        InitQige();//初始化qige[]
    }
    //private int _curUid = 1;
    public int []changshang= { -1, -1, -1, -1, -1, -1, -1, -1, -1 };//记录场上棋子的index
    public int []changxia= { -1, -1, -1, -1, -1, -1, -1, -1,-1 };//记录场下棋子的index
    public List<EntityQizi> QiziList = new List<EntityQizi>();//保存所有生成的棋子
    public List<EntityQizi> QiziCSList = new List<EntityQizi>();//保存所有在场上的棋子
    public List<EntityQizi> QiziCXList = new List<EntityQizi>();//保存所有在场下的棋子
    public List<EntityQizi> DirenList = new List<EntityQizi>();//保存敌方棋子
    public int[] qizi = {1 };//记录棋子的价格，i是棋子的index
    public int[] qizishu = {20 };//i是棋子的index，里面是棋子是剩余数量
    //保存每种费用棋子的数量，index以及每个等级抽到的概率
    int paiku1num=0;
    int[] paiku1 = new int[100];
    int[] gailv1 = {100,70,60,50,40,30,20,10,5};
    int paiku2num = 0;
    int[] paiku2;
    int[] gailv2 = { 0,30,35,40,35,30,30,25,20};
    int paiku3num = 0;
    int[] paiku3;
    int[] gailv3 = { 0, 0,5,10,20,30,30,30,25};
    int paiku4num = 0;
    int[] paiku4;
    int[] gailv4 = { 0, 0,0,0,5,10,15,25,30};
    int paiku5num = 0;
    int[] paiku5;
    int[] gailv5 = { 0, 0,0,0, 0,0,5,10,20};
    public int []goumaiUIqiziID = new int[5];//记录UI购买界面的棋子index
    public int []goumaiUIqiziPaikuIndex = new int[5];//记录UI购买界面的棋子在牌库的index
    private int[] goumaiUiqiziPaikuFeiyong = new int[5];//记录UI购买界面的棋子
    public int dangqianliucheng = 0;//保存当前流程，0是prebattle,1是battle
    public List<Sprite> ListQiziLevelSprite = new List<Sprite>();//保存棋子星级图片
    public int[][] qige = new int[8][];//保存棋格上是否有棋子 -1表示没有，其余是棋子uid 坐标是y，x
    public Vector3[][] qigepos = new Vector3[8][];//保存棋格位置
    public float qigeXOffset = 1f;
    public Vector3[] cxqigepos = new Vector3[9];//保存场下棋格位置
    public int QiziCurUniqueIndex = 0;
    public GameObject showpath;
    private Vector2Int[] adjacentOffsetDoubleRow = new Vector2Int[6]
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.one, Vector2Int.down + Vector2Int.right
    };
    private Vector2Int[] adjacentOffsetSingleRow = new Vector2Int[6]
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.left+Vector2Int.up, Vector2Int.left+ Vector2Int.down
    };
    void Init()//初始化每个牌库
    {
        showpath = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/qigezi/showpath.prefab");//绑定path预制体
        Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Image/level1.png");
        Sprite spr2 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Image/level2.png");
        Sprite spr3 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Image/level3.png");
        ListQiziLevelSprite.Add(spr);
        ListQiziLevelSprite.Add(spr2);
        ListQiziLevelSprite.Add(spr3);
        for (int i=0;i<qizi.Length;i++)
        {
            switch (qizi[i])
            {
                case 1:
                    for (int j = 0; j < qizishu[i]; j++)
                    {
                        paiku1[paiku1num] = i+1;
                        paiku1num++;
                    }
                    break;
                case 2:
                    for (int j = 0; j < qizishu[i]; j++)
                    {
                        paiku2[paiku2num] = i+1;
                        paiku2num++;
                    }
                    break;
                case 3:
                    for (int j = 0; j < qizishu[i]; j++)
                    {
                        paiku3[paiku3num] = i+1;
                        paiku3num++;
                    }
                    break;
                case 4:
                    for (int j = 0; j < qizishu[i]; j++)
                    {
                        paiku4[paiku4num] = i+1;
                        paiku4num++;
                    }
                    break;
                case 5:
                    for (int j = 0; j < qizishu[i]; j++)
                    {
                        paiku5[paiku5num] = i+1;
                        paiku5num++;
                    }
                    break;
                default:
                    break;

            }
        }
    }
    public void InitDirenList()
    {
        EntityQizi qizi = Pool.instance.PoolEntity.Get() as EntityQizi;
        qizi.BelongCamp = CampType.Enemy;
        qizi.Init(1);
        QiziCXList.Remove(qizi);
        QiziList.Remove(qizi);
        qizi.GObj.SetActive(false);
        qizi.rowIndex =5;
        qizi.columnIndex = 1;
        DirenList.Add(qizi);

        EntityQizi qizi2 = Pool.instance.PoolEntity.Get() as EntityQizi;
        qizi.BelongCamp = CampType.Enemy;
        qizi2.Init(2);
        QiziCXList.Remove(qizi2);
        QiziList.Remove(qizi2);
        qizi2.GObj.SetActive(false);
        qizi2.rowIndex = 6;
        qizi2.columnIndex = 5;
        DirenList.Add(qizi2);
        //EntityQizi qizi3 = Pool.instance.PoolEntity.Get() as EntityQizi;
        //qizi3.Init(0);
        //QiziCXList.Remove(qizi3);
        //QiziList.Remove(qizi3);
        //qizi3.GObj.SetActive(false);
        //qizi3.x = -1.414214f;
        //qizi3.y = -1.414214f;
        //DirenList.Add(qizi3);
    }

    public void InitOneEnemy(OneEnemyInfo oneInfo)
    {
        EntityQizi qizi = Pool.instance.PoolEntity.Get() as EntityQizi;
        qizi.BelongCamp = CampType.Enemy;
        qizi.Init(oneInfo.HeroID);
        qizi.rowIndex =oneInfo.Pos.y;
        qizi.columnIndex = oneInfo.Pos.x;
        DirenList.Add(qizi);
        qige[qizi.rowIndex][qizi.columnIndex] = qizi.HeroUID;
        qizi.LogicPosition =GetGeziPos(qizi.rowIndex, qizi.columnIndex);
        qizi.GObj.transform.rotation = Quaternion.Euler(new Vector3(0, -180, 0));
    }

    public EntityQizi AddNewFriendHero(int heroID)
    {
        EntityQizi qizi = Pool.instance.PoolEntity.Get() as EntityQizi;
        qizi.BelongCamp = CampType.Friend;
        qizi.Init(heroID);
        var emptyPos = GetEmptyFriendPos();
        qizi.rowIndex =emptyPos.y;
        qizi.columnIndex = emptyPos.x;
        QiziCSList.Add(qizi);
        qige[qizi.rowIndex][qizi.columnIndex] = qizi.HeroUID;
        qizi.LogicPosition =GetGeziPos(qizi.rowIndex, qizi.columnIndex);
        return qizi;
    }

    private Vector2Int GetEmptyFriendPos()
    {
        for (int y = 0; y < qige.Length; y++)
        {
            for (int x = 0; x < qige[y].Length; x++)
            {
                if (qige[y][x] == -1)
                {
                    return new Vector2Int(x, y);
                }
            }
        }
        return Vector2Int.one;
    }
    void InitQige()
    {
        qige[0] = new int[7] { -1, -1, -1, -1, -1 ,-1,-1};
        qige[1] = new int[7] { -1, -1, -1, -1, -1 ,-1,-1};
        qige[2] = new int[7] { -1, -1, -1, -1, -1 ,-1,-1};
        qige[3] = new int[7] { -1, -1, -1, -1, -1 ,-1,-1};
        qige[4] = new int[7] { -1, -1, -1, -1, -1 ,-1,-1};
        qige[5] = new int[7] { -1, -1, -1, -1, -1 ,-1,-1};
        qige[6] = new int[7] { -1, -1, -1, -1, -1 ,-1,-1};
        qige[7] = new int[7] { -1, -1, -1, -1, -1 ,-1,-1};
        qigepos[0] = new Vector3[7];
        qigepos[1] = new Vector3[7];
        qigepos[2] = new Vector3[7];
        qigepos[3] = new Vector3[7];
        qigepos[4] = new Vector3[7];
        qigepos[5] = new Vector3[7];
        qigepos[6] = new Vector3[7];
        qigepos[7] = new Vector3[7];
    }
    //按照等级来抽五张牌
    public void choupai(int dengji)
    {
        int sumnum = 0;
        for (int i=0;i<5;i++)
        {
            goumaiUIqiziID[i] = -1;
            goumaiUIqiziPaikuIndex[i] = -1;
        }
        for (int i=0;i<5&&sumnum<1000;i++)
        {
            sumnum++;
            int dengjirandom = UnityEngine.Random.Range(0,100);
            if (dengjirandom <= gailv1[dengji-1])
            {
                if (paiku1num != 0)
                {
                    int pairandom = findRandom(i, paiku1num, paiku1,1);
                    goumaiUIqiziID[i] = paiku1[pairandom];
                    goumaiUIqiziPaikuIndex[i] = pairandom;
                }
                else 
                {
                    i--;
                }
            }
            else if (dengjirandom <= gailv1[dengji - 1] + gailv2[dengji - 1])
            {
                if (paiku2num != 0)
                {
                    int pairandom = findRandom(i,paiku2num,paiku2,2);
                    goumaiUIqiziID[i] = paiku2[pairandom];
                    goumaiUIqiziPaikuIndex[i] = pairandom;
                }
                else
                {
                    i--;
                }
            }
            else if (dengjirandom <= gailv1[dengji - 1] + gailv2[dengji - 1] + gailv3[dengji - 1])
            {
                if (paiku3num != 0)
                {
                    int pairandom = findRandom(i, paiku3num, paiku3,3);
                    goumaiUIqiziID[i] = paiku3[pairandom];
                    goumaiUIqiziPaikuIndex[i] = pairandom;
                }
                else
                {
                    i--;
                }
            }
            else if (dengjirandom <= gailv1[dengji - 1] + gailv2[dengji - 1] + gailv3[dengji - 1] + gailv4[dengji - 1])
            {
                if (paiku4num != 0)
                {
                    int pairandom = findRandom(i, paiku4num, paiku4,4);
                    goumaiUIqiziID[i] = paiku4[pairandom];
                    goumaiUIqiziPaikuIndex[i] = pairandom;
                }
                else
                {
                    i--;
                }
            }
            else
            {
                if (paiku5num != 0)
                {
                    int pairandom = findRandom(i, paiku5num, paiku5,5);
                    goumaiUIqiziID[i] = paiku5[pairandom];
                    goumaiUIqiziPaikuIndex[i] = pairandom;
                }
                else
                {
                    i--;
                }
            }
        }
    }
    int findRandom(int i,int paikunum,int[]paiku,int feiyong)
    {
        int pairandom = UnityEngine.Random.Range(0, paikunum);
        bool findYou = true;
        while (findYou)
        {
            findYou = false;
            for (int j = 0; j < i; j++)
            {
                if (pairandom == goumaiUIqiziID[i] && qizi[paiku[pairandom]] == feiyong)
                {
                    findYou = true;
                    pairandom = UnityEngine.Random.Range(0, paikunum);
                }
            }
        }
        return pairandom;
    }
    public int findkongweiCS()//发现场上空位
    {
        for (int i=0;i<9;i++)
        {
            if (changshang[i]==-1)
            {
                return i;
            }
        }
        return -1;
    }
    public int findkongweiCX()
    {
        for (int i = 0; i < 9; i++)
        {
            if (changxia[i] == -1)
            {
                return i;
            }
        }
        return -1;
    }
    public void goumaiqizi(int qiziindex,int paikuindex,int kongweiCX,int feiyong)
    {
        changxia[kongweiCX] = qiziindex;
        if (feiyong == 1)
        {
            paiku1[paikuindex] = paiku1[paiku1num - 1];
            paiku1[paiku1num - 1] = qiziindex;
            paiku1num--;
        }
        else if (feiyong == 2)
        {
            paiku2[paikuindex] = paiku2[paiku2num - 1];
            paiku2[paiku2num - 1] = qiziindex;
            paiku2num--;
        }
        else if (feiyong == 3)
        {
            paiku3[paikuindex] = paiku3[paiku3num - 1];
            paiku3[paiku1num - 1] = qiziindex;
            paiku3num--;
        }
        else if (feiyong == 4)
        {
            paiku4[paikuindex] = paiku4[paiku4num - 1];
            paiku4[paiku1num - 1] = qiziindex;
            paiku4num--;
        }
        else
        {
            paiku5[paikuindex] = paiku5[paiku5num - 1];
            paiku5[paiku5num - 1] = qiziindex;
            paiku5num--;
        }
    }
    public void chushouQizi(int qiziindex,int feiyong,int level)//卖棋子
    {
        int qizinum = (int)Mathf.Pow(3, level - 1);
        if (feiyong/ qizinum == 1)
        {
            for (int i=0;i<qizinum;i++)
            {
                paiku1[paiku1num] = qiziindex;
                paiku1num++;
            }
        }
        else if ((feiyong + 1) / qizinum == 2)
        {
            for (int i = 0; i < qizinum; i++)
            {
                paiku2[paiku2num] = qiziindex;
                paiku2num++;
            }
        }
        else if ((feiyong + 1) / qizinum == 3)
        {
            for (int i = 0; i < qizinum; i++)
            {
                paiku3[paiku3num] = qiziindex;
                paiku3num++;
            }
        }
        else if ((feiyong + 1) / qizinum == 4)
        {
            for (int i = 0; i < qizinum; i++)
            {
                paiku4[paiku4num] = qiziindex;
                paiku4num++;
            }
        }
        else
        {
            for (int i = 0; i < qizinum; i++)
            {
                paiku5[paiku5num] = qiziindex;
                paiku5num++;
            }
        }
    }
    public float getDistance(Vector2Int a,Vector2Int b)
    {
        try
        {
            return (qigepos[a.y][a.x].x - qigepos[b.y][b.x].x) * (qigepos[a.y][a.x].x - qigepos[b.y][b.x].x) + (qigepos[a.y][a.x].z - qigepos[b.y][b.x].z) * (qigepos[a.y][a.x].z - qigepos[b.y][b.x].z);
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        return 0;
    }
    public Vector2Int getIndexQige(Vector3 pos)
    {
        int targetx = 4 - (int)(pos.z * 2 / 1.414f );
        int targety;
        if (targetx % 2 == 0)
        {
            targety = (int)(pos.x / 1.414f) + 2;
        }
        else
        {
            targety = (int)((pos.x + 1.414f / 2) / 1.414f) + 2;
        }
        return new Vector2Int(targetx,targety);
    }

    public Vector2Int GetIndexQizi(EntityQizi qizi)
    {
        var qiziUid = qizi.HeroUID;
        for (var rowIndex = 0; rowIndex < qige.Length; rowIndex++)
        {
            var oneline = qige[rowIndex];
            for (int columnIndex = 0; columnIndex < oneline.Length; columnIndex++)
            {
                if (qige[rowIndex][columnIndex] == qiziUid)
                {
                    return new Vector2Int(columnIndex,rowIndex);
                }
            }
        }
        return new Vector2Int(-1, -1);
    }

    public bool GetQiziByQigeIndex(Vector2Int geziPos,out EntityQizi curPosQizi)
    {
        curPosQizi = null;
        try
        {
            var uid = qige[geziPos.y][geziPos.x];
            foreach (var oneQizi in QiziCSList)
            {
                if (oneQizi.HeroUID == uid)
                {
                    curPosQizi = oneQizi;
                    return true;
                }
            }

            foreach (var oneQizi in DirenList)
            {
                if (oneQizi.HeroUID == uid)
                {
                    curPosQizi = oneQizi;
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        return false;
    }

    public void UpdateEntityPos(EntityQizi qizi, Vector2Int newPos)
    {
        qige[qizi.rowIndex][qizi.columnIndex] = -1;
        qige[newPos.y][newPos.x] = qizi.HeroUID;
        qizi.LogicPosition = qigepos[newPos.y][newPos.x];
    }
    public Vector2Int Findpath(Vector2Int start, Vector2Int end,float gongjidistance)
    {
        /*if (gongjidistance == 1&&IsSurround(end))//攻击距离为1，先判断是否目标被围了一圈
        {
            return new Vector2Int(-1,-1);
        }*/
        Vector2Int lastpos=start;
        List<Vector2Int> sortList = new List<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> xiaoHao = new Dictionary<Vector2Int, float>();
        sortList.Add(start);
        //cameFrom[start] = new Vector2Int(-1,-1);
        xiaoHao[start] = 0f;
        bool find = false;
        while (sortList.Count > 0)
        {
            sortList.Sort((a,b)=>(xiaoHao[a]*xiaoHao[a]+getDistance(a, end)).CompareTo((xiaoHao[b]*xiaoHao[b]+getDistance(b, end))));
            Vector2Int current = sortList[0];
            sortList.Remove(current);
            if (getDistance(current,end)<=gongjidistance*gongjidistance)
            {
                lastpos = current;
                find = true;
                break;
            }

            var adjacentOffList = current.y % 2 == 0 ? adjacentOffsetDoubleRow : adjacentOffsetSingleRow;
            for (int adjacentCellIndex = 0; adjacentCellIndex < 6; adjacentCellIndex++)
            {
                var curAdjacent = current + adjacentOffList[adjacentCellIndex];
                if (curAdjacent.x < 0 || curAdjacent.x > 6 || curAdjacent.y < 0 || curAdjacent.y > 7)
                {
                    continue;
                }
                if (cameFrom.ContainsKey(curAdjacent)&&xiaoHao[curAdjacent] <= xiaoHao[current] + 1)//找到了更短到达newPos的路径
                {
                    continue;
                }
                if (QiziGuanLi.Instance.qige[curAdjacent.y][curAdjacent.x] >-1)
                {
                    continue;
                }
                sortList.Add(curAdjacent);
                cameFrom[curAdjacent] = current;
                xiaoHao[curAdjacent] = xiaoHao[current] + 1;
            }
            /*//找四个斜线方向的点,x为奇数或者偶数情况不一样
            if (current.x % 2 == 0)//偶数情况
            {
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        Vector2Int newpos = new Vector2Int(current.x + 1 - i * 2, current.y + j);
                        if (newpos.x < 0 || newpos.x > 8 || newpos.y < 0 || newpos.y > 5)
                        {
                            continue;
                        }
                        if (cameFrom.ContainsKey(newpos)&&xiaoHao[newpos] <= xiaoHao[current] + 1)//找到了更短到达newPos的路径
                        {
                            continue;
                        }
                        if (QiziGuanLi.Instance.qige[newpos.x][newpos.y] >-1)
                        {
                            continue;
                        }
                        sortList.Add(newpos);
                        cameFrom[newpos] = current;
                        xiaoHao[newpos] = xiaoHao[current] + 1;
                    }
                }
            }
            else//奇数情况
            {
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        Vector2Int newpos = new Vector2Int(current.x + 1 - i * 2, current.y - j);

                        if (newpos.x < 0 || newpos.x > 8 || newpos.y < 0 || newpos.y > 4)
                        {
                            continue;
                        }
                        if (cameFrom.ContainsKey(newpos)&&xiaoHao[newpos] <= xiaoHao[current] + 1)//找到了更短到达newPos的路径
                        {
                            continue;
                        }
                        if (QiziGuanLi.Instance.qige[newpos.x][newpos.y] >-1)
                        {
                            continue;
                        }
                        sortList.Add(newpos);
                        cameFrom[newpos] = current;
                        xiaoHao[newpos] = xiaoHao[current] + 1;
                    }
                }
            }*/
        }
        //Log.Info("hfk:find ="+find);
        if (find)
        {
            Stack<Vector2Int> trace = new Stack<Vector2Int>();
            Vector2Int pos = lastpos;
            while (pos!=start)
            {
                trace.Push(pos);
                pos = cameFrom[pos];
            }
            while (trace.Count > 0)
            {
                Vector2Int p = trace.Pop();
                return p;
                //Vector3 po = qigepos[p.x][p.y];
                //Log.Info("hfk:p=" + qigepos[p.x][p.y].x+" "+ qigepos[p.x][p.y].z + " pos=" + po);
                //GameObject qg =GameObject.Instantiate(showpath, po, Quaternion.Euler(0, 45, 0));
            }
            return lastpos;
        }
        return new Vector2Int(-1,-1);
    }
    public bool IsSurround(Vector2Int target)
    {
        //找四个斜线方向的点,x为奇数或者偶数情况不一样
        if (target.x % 2 == 0)//偶数情况
        {
            
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    Vector2Int newpos = new Vector2Int(target.x + 1 - i * 2, target.y + j);
                    if (newpos.x < 0 || newpos.x > 8 || newpos.y < 0 || newpos.y > 5)
                    {
                        continue;
                    }
                    if (QiziGuanLi.Instance.qige[newpos.x][newpos.y] >-1)
                    {
                        continue;
                    }
                    return false;
                }
            }
        }
        else//奇数情况
        {
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    Vector2Int newpos = new Vector2Int(target.x + 1 - i * 2, target.y - j);

                    if (newpos.x < 0 || newpos.x > 8 || newpos.y < 0 || newpos.y > 4)
                    {
                        continue;
                    }
                    if (QiziGuanLi.Instance.qige[newpos.x][newpos.y] >-1)
                    {
                        continue;
                    }
                    return false;
                }
            }
        }

        return true;
    }

    public Vector3 GetGeziPos(int row,int column)
    {
        return row >= 0 ? qigepos[row][column] : cxqigepos[column];
    }

    public bool CheckInGezi(Vector3 targetPos, out Vector2Int geziPos)
    {
        for (var rowIndex = 0; rowIndex < qigepos.Length; rowIndex++)
        {
            var oneline = qigepos[rowIndex];
            for (int columnIndex = 0; columnIndex < oneline.Length; columnIndex++)
            {
                 var qigeCenterToTargetPos = targetPos - qigepos[rowIndex][columnIndex];
                 if (qigeCenterToTargetPos.magnitude > qigeXOffset / Mathf.Sqrt(3))//在外圆外
                 {
                     continue;
                 }
                 var absX = Mathf.Abs(qigeCenterToTargetPos.x);
                 var absY =  Mathf.Abs(qigeCenterToTargetPos.z);
                 if (absX > qigeXOffset / 2)
                 {
                     continue;
                 }

                 if (absY < qigeXOffset / 2 / Mathf.Sqrt(3))
                 {
                     geziPos = new Vector2Int(columnIndex, rowIndex);
                     return true;
                 }

                 if (qigeCenterToTargetPos.magnitude <= qigeXOffset / 2)//在内圈内
                 {
                     geziPos = new Vector2Int(columnIndex, rowIndex);
                     return true;
                 }

                 if ((qigeXOffset / 2 - absX) / Mathf.Sqrt(3) + qigeXOffset / 2 / Mathf.Sqrt(3) > absY)
                 {
                     geziPos = new Vector2Int(columnIndex, rowIndex);
                     return true;
                 }
            }
        }
        geziPos = Vector2Int.zero;
        return false;
    }
    /// <summary>
    /// 逻辑update
    /// </summary>
    /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
    /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
    public void OnLogicUpdate(float elapseSeconds, float realElapseSeconds)
    {
        List<EntityQizi> tempEntityList = ListPool<EntityQizi>.Get();
        //先轮询己方棋子，后续联机的话需要判断 玩家uid来确定先后
        tempEntityList.AddRange(QiziCSList);
        tempEntityList.Sort((a,b)=>a.HeroUID.CompareTo(b.HeroUID));
        foreach (var oneEntity in tempEntityList)
        {
            oneEntity.OnLogicUpdate(elapseSeconds, realElapseSeconds);
        }
        tempEntityList.Clear();
        tempEntityList.AddRange(DirenList);
        tempEntityList.Sort((a,b)=>a.HeroUID.CompareTo(b.HeroUID));
        foreach (var oneEntity in tempEntityList)
        {
            oneEntity.OnLogicUpdate(elapseSeconds, realElapseSeconds);
        }
        ListPool<EntityQizi>.Release(tempEntityList);
        OnLogicUpdateBullet(elapseSeconds, realElapseSeconds);
    }

    public void OnEntityDead(EntityQizi qizi)
    {
        if (qizi == null)
        {
            return;
        }
        qige[qizi.rowIndex][qizi.columnIndex] = -1;
        List<EntityQizi> qiziList = qizi.BelongCamp == CampType.Friend ? QiziCSList : DirenList;
        qiziList.Remove(qizi);
        if (qiziList.Count == 0)
        {
            GameEntry.Event.FireNow(this,BattleStopEventArgs.Create(qizi.BelongCamp==CampType.Enemy));
        }
    }
    /// <summary>
    /// 游戏结束回到主界面
    /// </summary>
    public void GameOver()
    {
        FreshQige();
        foreach (var oneEntity in QiziCSList)
        {
            oneEntity.Remove();
        }
        QiziCSList.Clear();
        foreach (var oneEntity in DirenList)
        {
            oneEntity.Remove();
        }
        DirenList.Clear();
        dangqianliucheng = 0;
    }

    private void FreshQige()
    {
        for (int y = 0; y < qige.Length; y++)
        {
            for (int x = 0; x < qige[y].Length; x++)
            {
                qige[y][x] = -1;
            }
        }
    }
}
