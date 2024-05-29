using GameFramework.Fsm;
using GameFramework.ObjectPool;
using Entity;
using NUnit;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

using UnityEngine.Pool;
using UnityEngine.TextCore.Text;
using UnityGameFramework.Runtime;

public class Pool :MonoBehaviour
{
    public static Pool instance;//单实例化

    public ObjectPool<EntityBase> PoolEntity;
    public ObjectPool<GameObject> pool;
    public Dictionary<int, ObjectPool<GameObject>> PoolObject = new Dictionary<int, ObjectPool<GameObject>>();
    public List<EntityQizi> list = new List<EntityQizi>();//存放生成的entityqizi
    void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        PoolEntity = new ObjectPool<EntityBase>(createFuncEnt, actionOnGetEnt, actionOnReleaseEnt, actionOnDestroyEnt, true, 10, 1000);
        pool=new ObjectPool<GameObject>(createFuncObj, actionOnGetObj, actionOnReleaseObj, actionOnDestroyObj, true, 10, 1000);
        PoolObject.Add(0,pool);
    }

    EntityBase createFuncEnt()
    {
        EntityBase ent = new EntityQizi();
        return ent;
    }
    GameObject createFuncObj()
    {
        GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/Charactor/qizi0/unitychan.prefab"); 
        GameObject ob = Instantiate(obj,this.transform);
        return ob;
    }

    void actionOnGetEnt(EntityBase ent)
    {
        list.Add(ent as EntityQizi);
        //ent.GObj.transform.SetParent(this.transform);
        //ent.GObj.transform.localPosition = new Vector3(UnityEngine.Random.Range(-5, 5), 0, 0);
        //ent.GObj.gameObject.SetActive(true);
    }
    void actionOnGetObj(GameObject obj)
    {
        //obj.transform.SetParent(this.transform);
        //obj.GetComponentInParent<EntityQizi>().GObj.transform.SetParent(this.transform);

        obj.transform.localPosition = new Vector3(UnityEngine.Random.Range(-5, 5), 0, 0);
        obj.gameObject.SetActive(true);
    }
    void actionOnReleaseEnt(EntityBase ent)
    {
        ent.GObj.gameObject.SetActive(false);
    }
    void actionOnReleaseObj(GameObject obj)
    {
        obj.gameObject.SetActive(false);
    }
    void actionOnDestroyEnt(EntityBase ent)
    {
        Destroy(ent.GObj);
    }
    void actionOnDestroyObj(GameObject obj)
    {
        Destroy(obj);
    }
}
