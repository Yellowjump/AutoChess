using GameFramework.ObjectPool;
using liuchengguanli;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Pool;

public class PoolObject : MonoBehaviour
{
    public static PoolObject instance;//µ¥ÊµÀý»¯
     void Awake()
    {
        instance = this;
    }


    public EntityBase entity;
    ObjectPool<EntityBase> pool;
    public Dictionary<int, ObjectPool<EntityBase>> Pool = new Dictionary<int, ObjectPool<EntityBase>>();
    private void Start()
    {
        pool = new ObjectPool<EntityBase>(createFunc, actionOnGet, actionOnRelease, actionOnDestroy, true, 10, 1000);
        Pool.Add(0,pool);
    }

    EntityBase createFunc()
    {
        var ent = Instantiate(entity.GObj,transform);
        return ent.GetComponent<EntityBase>();
    }
    void actionOnGet(EntityBase ent)
    {
        ent.GObj.transform.SetParent(this.transform);
        ent.GObj.transform.localPosition = new Vector3(UnityEngine.Random.Range(-5, 5), 0, 0);
        ent.GObj.gameObject.SetActive(true);
    }
    void actionOnRelease(EntityBase ent)
    {
        ent.GObj.gameObject.SetActive(false);
    }
    void actionOnDestroy(EntityBase ent)
    {
        Destroy(ent.GObj);
    }
}
