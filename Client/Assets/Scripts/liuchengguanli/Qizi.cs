using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Qizi
{
    private int _curUid = 1;
    int []changshang= { 0,0,0,0,0,0,0,0,0};
    int []changxia= { 0,0,0,0,0,0,0,0};
    int findkongweiCS()
    {
        for (int i=0;i<9;i++)
        {
            if (changshang[i]==0)
            {
                return i;
            }
        }
        return -1;
    }
    int findkongweiCX()
    {
        for (int i = 0; i < 8; i++)
        {
            if (changxia[i] == 0)
            {
                return i;
            }
        }
        return -1;
    }
    void goumaiqizi(int qiziindex,int kongweiCX,int kongweiCS)
    {
        if (kongweiCX != -1)
        {
            changxia[kongweiCX] = qiziindex;
        }
        else
        {
            changshang[kongweiCS] = qiziindex;
        }
    }

}
