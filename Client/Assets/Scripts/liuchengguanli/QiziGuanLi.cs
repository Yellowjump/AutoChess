using liuchengguanli;
using System;
using System.Collections;
using System.Collections.Generic;
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
    public int []goumaiUIqiziIndex = new int[5];//记录UI购买界面的棋子index
    public int []goumaiUIqiziPaikuIndex = new int[5];//记录UI购买界面的棋子在牌库的index
    private int[] goumaiUiqiziPaikuFeiyong = new int[5];//记录UI购买界面的棋子
    public int dangqianliucheng = 0;//保存当前流程，0是prebattle,1是battle
    public List<Sprite> ListQiziLevelSprite = new List<Sprite>();//保存棋子星级图片
    public int[][] qige = new int[9][];//保存棋格上是否有棋子 -1表示没有，其余是棋子uid
    public Vector3[][] qigepos = new Vector3[9][];//保存棋格位置
    public int QiziCurUniqueIndex = 0;
    public GameObject showpath;

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
                        paiku1[paiku1num] = i;
                        paiku1num++;
                    }
                    break;
                case 2:
                    for (int j = 0; j < qizishu[i]; j++)
                    {
                        paiku2[paiku2num] = i;
                        paiku2num++;
                    }
                    break;
                case 3:
                    for (int j = 0; j < qizishu[i]; j++)
                    {
                        paiku3[paiku3num] = i;
                        paiku3num++;
                    }
                    break;
                case 4:
                    for (int j = 0; j < qizishu[i]; j++)
                    {
                        paiku4[paiku4num] = i;
                        paiku4num++;
                    }
                    break;
                case 5:
                    for (int j = 0; j < qizishu[i]; j++)
                    {
                        paiku5[paiku5num] = i;
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
        qizi.Init(0);
        QiziCXList.Remove(qizi);
        QiziList.Remove(qizi);
        qizi.GObj.SetActive(false);
        qizi.x = -0.7071068f;
        qizi.y = -0.7071068f;
        DirenList.Add(qizi);

        EntityQizi qizi2 = Pool.instance.PoolEntity.Get() as EntityQizi;
        qizi.BelongCamp = CampType.Enemy;
        qizi2.Init(0);
        QiziCXList.Remove(qizi2);
        QiziList.Remove(qizi2);
        qizi2.GObj.SetActive(false);
        qizi2.x = 3.535534f;
        qizi2.y = 2.12132f;
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
    void InitQige()
    {
        qige[0] =new int[5]{ -1,-1,-1,-1,-1};
        qige[1] = new int[6] { -1, -1, -1, -1, -1 ,-1};
        qige[2] = new int[5] { -1, -1, -1, -1, -1 };
        qige[3] = new int[6] { -1, -1, -1, -1, -1,-1 };
        qige[4] = new int[5] { -1, -1, -1, -1, -1 };
        qige[5] = new int[6] { -1, -1, -1, -1, -1,-1 };
        qige[6] = new int[5] { -1, -1, -1, -1, -1 };
        qige[7] = new int[6] { -1, -1, -1, -1, -1,-1 };
        qige[8] = new int[5] { -1, -1, -1, -1, -1 };
        qigepos[0] = new Vector3[5];
        qigepos[1] = new Vector3[6];
        qigepos[2] = new Vector3[5];
        qigepos[3] = new Vector3[6];
        qigepos[4] = new Vector3[5];
        qigepos[5] = new Vector3[6];
        qigepos[6] = new Vector3[5];
        qigepos[7] = new Vector3[6];
        qigepos[8] = new Vector3[5];

    }
    //按照等级来抽五张牌
    public void choupai(int dengji)
    {
        int sumnum = 0;
        for (int i=0;i<5;i++)
        {
            goumaiUIqiziIndex[i] = -1;
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
                    goumaiUIqiziIndex[i] = paiku1[pairandom];
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
                    goumaiUIqiziIndex[i] = paiku2[pairandom];
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
                    goumaiUIqiziIndex[i] = paiku3[pairandom];
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
                    goumaiUIqiziIndex[i] = paiku4[pairandom];
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
                    goumaiUIqiziIndex[i] = paiku5[pairandom];
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
                if (pairandom == goumaiUIqiziIndex[i] && qizi[paiku[pairandom]] == feiyong)
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
            return (qigepos[a.x][a.y].x - qigepos[b.x][b.y].x) * (qigepos[a.x][a.y].x - qigepos[b.x][b.y].x) + (qigepos[a.x][a.y].z - qigepos[b.x][b.y].z) * (qigepos[a.x][a.y].z - qigepos[b.x][b.y].z);
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
        for (var index = 0; index < qige.Length; index++)
        {
            var oneline = qige[index];
            for (int lineIndex = 0; lineIndex < oneline.Length; lineIndex++)
            {
                if (qige[index][lineIndex] == qiziUid)
                {
                    return new Vector2Int(index, lineIndex);
                }
            }
        }
        return new Vector2Int(-1, -1);
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
            //找四个斜线方向的点,x为奇数或者偶数情况不一样
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
            }
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
    /// <summary>
    /// 逻辑update
    /// </summary>
    /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
    /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
    public void OnLogicUpdate(float elapseSeconds, float realElapseSeconds)
    {
        List<EntityQizi> tempEntityList = ListPool<EntityQizi>.Get();
        //先轮询己方棋子，后续联机的话需要判断 玩家uid来确定先后
        tempEntityList.AddRange(QiziList);
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
    }
}
