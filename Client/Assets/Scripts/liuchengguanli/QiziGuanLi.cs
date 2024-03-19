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

    //private int _curUid = 1;
    int []changshang= { -1, -1, -1, -1, -1, -1, -1, -1, -1 };//记录场上棋子的index
    int []changxia= { -1, -1, -1, -1, -1, -1, -1, -1,-1 };//记录场下棋子的index

    public int[] qizi = {1 };//记录棋子的价格，i是棋子的index
    public int[] qizishu = {20 };//i是棋子的index，里面是棋子是剩余数量

    int[] beixuan = new int[100];
    public int []goumaiUIqiziIndex = new int[5];//记录UI购买界面的棋子index

    public int findkongweiCS()
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
    public void goumaiqizi(int qiziindex,int kongweiCX)
    {
        if (kongweiCX != -1)
        {
            changxia[kongweiCX] = qiziindex;
        }
    }

}
