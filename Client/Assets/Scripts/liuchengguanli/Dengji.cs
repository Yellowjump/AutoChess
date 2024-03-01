using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dengji 
{
    //初始等级为1,经验为0
    private int dj = 1;
    private int jinyan = 0;
    private int []shengjixuqiu = {2,4,8,10,20,32,48,60};
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
}
