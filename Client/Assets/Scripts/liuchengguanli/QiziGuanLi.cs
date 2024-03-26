using liuchengguanli;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class QiziGuanLi
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
    }
    //private int _curUid = 1;
    public int []changshang= { -1, -1, -1, -1, -1, -1, -1, -1, -1 };//记录场上棋子的index
    public int []changxia= { -1, -1, -1, -1, -1, -1, -1, -1,-1 };//记录场下棋子的index
    public List<EntityQizi> QiziList = new List<EntityQizi>();//保存所有生成的棋子
    public List<EntityQizi> QiziCSList = new List<EntityQizi>();//保存所有在场上的棋子
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
    void Init()//初始化每个牌库
    {
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
    //按照等级来抽五张牌
    public void choupai(int dengji)
    {
        for (int i=0;i<5;i++)
        {
            int dengjirandom = Random.Range(0,100);
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
        int pairandom = Random.Range(0, paikunum);
        bool findYou = true;
        while (findYou)
        {
            findYou = false;
            for (int j = 0; j < i; j++)
            {
                if (pairandom == goumaiUIqiziIndex[i] && qizi[paiku[pairandom]] == feiyong)
                {
                    findYou = true;
                    pairandom = Random.Range(0, paikunum);
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
    public void chushouQizi()//卖棋子
    {

    }

}
