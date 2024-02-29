using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Jinqian
{
    //初始金币为0
    private int jinbinum = 0;

    public void changejinqian(int change)
    {
        jinbinum += change;
    }
    public void huihejiesuanJQ()
    {
        jinbinum += 5;//每回合固定有5金币
        //计算利息
        if (jinbinum >= 50)
        {
            jinbinum += 5;
        }
        else
        {
            jinbinum += (jinbinum / 10);
        }
    }
}
