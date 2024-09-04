using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dengji 
{
    public static Dengji instance;//单实例化
    public static Dengji Instance
    {
        get
        {
            if (instance == null)
                instance = new Dengji();
            return instance;
        }
    }
    //初始等级为1,经验为0,最高为9
    private int dj = 1;
    public int jinyan = 0;
    public int []shengjixuqiu = {2,4,8,10,20,32,48,60};
    //每回合给2经验
    public void huihejiesuanJY()
    {
        jinyan += 2;
        if (jinyan >=shengjixuqiu[dj])
        {
            jinyan -= shengjixuqiu[dj];
            dj++;
        }
    }
    //可以花金币购买经验
    public void changejinyan(int changenum)
    {
        jinyan += changenum;
        if (jinyan >= shengjixuqiu[dj])
        {
            jinyan -= shengjixuqiu[dj];
            dj++;
        }
    }
    public int getDj()
    {
        return dj;
    }
}
