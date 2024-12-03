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
using Cinemachine;
using DataTable;
using Entity;
using Entity.Bullet;
using Maze;
using SkillSystem;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        public Dictionary<int, CinemachineVirtualCamera> PerAreaCameraDic = new Dictionary<int, CinemachineVirtualCamera>();
        public CinemachineBrain CameraBrain;
        /// <summary>
        /// 初始化每个点的相机
        /// </summary>
        public void InitAreaPointCamera()
        {
            var areaList = SelfDataManager.Instance.CurAreaList;
            foreach (var oneAreaPoint in areaList)
            {
                var oneVirtualCameraObj = new GameObject($"AreaPointCamera_{oneAreaPoint.Index}");
                oneVirtualCameraObj.transform.parent = InstanceRoot;
                var vc = oneVirtualCameraObj.AddComponent<CinemachineVirtualCamera>();
                vc.m_Lens.FieldOfView = 60;
                vc.m_Lens.FarClipPlane = 2000;
                vc.m_Lens.NearClipPlane = 0.3f;
                vc.transform.position = oneAreaPoint.Pos + Vector3.up * 50;
                vc.transform.rotation = Quaternion.Euler(85,0,0);
                vc.Priority = 0;
                PerAreaCameraDic.Add(oneAreaPoint.Index,vc);
            }

            CameraBrain = Camera.main.GetComponent<CinemachineBrain>();
            var blend = CameraBrain.m_CustomBlends.m_CustomBlends;
        }

        public void CheckToAreaPointCamera(int areaIndex)
        {
            if (PerAreaCameraDic.ContainsKey(areaIndex))
            {
                ResetToMainCamera();
                var vc = PerAreaCameraDic[areaIndex];
                
                vc.Priority = 20;
            }
        }

        public void ResetToMainCamera()
        {
            foreach (var keyValue in PerAreaCameraDic)
            {
                var vc = keyValue.Value;
                vc.Priority = 0;
            }
        }
    }
}