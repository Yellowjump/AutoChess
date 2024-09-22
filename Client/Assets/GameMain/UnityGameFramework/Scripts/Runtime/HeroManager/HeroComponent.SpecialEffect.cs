//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using GameFramework;
using System.Collections.Generic;
using System.IO;
using DataTable;
using Entity;
using Entity.Bullet;
using Maze;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

namespace UnityGameFramework.Runtime
{
    public class SfxEntity:IReference
    {
        public EntityBase Owner;
        public GameObject GObj;
        [FormerlySerializedAs("Duration")] public int DurationMs;
        public Vector3 PosOffset= Vector3.zero;
        public int SfxID;
        public int ExistNum;
        public void Clear()
        {
            GameEntry.HeroManager.ReleaseSfxGameObject(SfxID, GObj, OnGetHeroGObjCallback);
            Owner = null;
            PosOffset= Vector3.zero;
            SfxID = 0;
            ExistNum = 0;
        }
        public void InitGObj()
        {
            GameEntry.HeroManager.GetSfxByID(SfxID,OnGetHeroGObjCallback);
        }
        protected virtual void OnGetHeroGObjCallback(GameObject obj)
        {
            GObj = obj;
            GObj.transform.SetParent(Owner.GObj.transform);
            GObj.transform.localPosition = PosOffset;
        }
    }
    public sealed partial class HeroComponent
    {
        
    }
}