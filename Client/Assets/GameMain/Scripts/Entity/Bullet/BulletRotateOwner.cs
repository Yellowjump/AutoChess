using System;
using System.Collections.Generic;
using GameFramework;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity.Bullet
{
    public class BulletRotateOwner:BulletBase
    {
        private Vector3 CenterLogicPosition;
        private float _curAngle = 0;//子弹的角度，（1，0）是0度 
        private float distanceFromCenter = 0;//每个旋转子弹到中心距离
        private float validRangeFromCenterMin = 0;//每个子弹有效范围最小值（0即圆心）
        private float validRangeFromCenterMax = 0;//每个子弹有效范围最大值
        private int _endSumRotateAngle = -1;//当旋转了多少度时子弹终止
        private float _curSumRotateAngle = 0;//当前共计旋转了多少度
        private int _rotateAngleSpeed = 1;//每秒旋转角度 顺时针为正
        public int StartAngle = 0;//初始的角度
        public override void SetParamValue(TableParamInt[] paramIntArray)
        {
            if (paramIntArray != null && paramIntArray.Length > 0)
            {
                StartAngle = paramIntArray[0].Value;
            }

            if (CurBulletData != null)
            {
                distanceFromCenter = CurBulletData.ParamInt1/1000.0f;
                validRangeFromCenterMin = CurBulletData.ParamInt2/1000.0f;
                validRangeFromCenterMax = CurBulletData.ParamInt3/1000.0f;
                _rotateAngleSpeed = CurBulletData.ParamInt4;
                _endSumRotateAngle= CurBulletData.ParamInt5;
            }
        }
        public override void InitGObj()
        {
            CenterLogicPosition = Target.LogicPosition;
            base.InitGObj();
            _curAngle = StartAngle;
        }
        protected override void OnGetHeroGObjCallback(GameObject obj)
        {
            GObj = obj;
            ComputeCurPosAndDir();
        }
        public override void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (Target == null||Caster.IsValid==false)
            {
                OnDead();
                return;
            }
            CenterLogicPosition = Target.LogicPosition;
            var changeAngle = _rotateAngleSpeed * elapseSeconds;
            var beforeAngle = _curAngle;
            _curAngle += changeAngle;
            _curAngle = Mathf.Repeat(_curAngle, 360);
            _curSumRotateAngle += changeAngle;
            ComputeCurPosAndDir();
            var heroList = GameEntry.HeroManager.GetEnemyList(BelongCamp);
            var beforeDir = new Vector2(Mathf.Cos(Mathf.Deg2Rad * beforeAngle), Mathf.Sin(Mathf.Deg2Rad * beforeAngle));
            //var curDir = new Vector2(Mathf.Cos(Mathf.Deg2Rad * _curAngle), Mathf.Sin(Mathf.Deg2Rad * _curAngle));
            foreach (var oneEntity in heroList)
            {
                if (oneEntity.IsValid == false)
                {
                    continue;
                }
                var targetDir = oneEntity.LogicPosition - CenterLogicPosition;
                var targetDirV2 = new Vector2(targetDir.x, targetDir.z);
                if (targetDirV2.magnitude > validRangeFromCenterMax||targetDirV2.magnitude < validRangeFromCenterMin)
                {
                    continue;
                }

                if (Math.Abs(changeAngle)>= 360)
                {
                    OwnerTriggerList?.OnTrigger(TriggerType.OnBulletHitTarget,oneEntity);
                }
                else
                {
                    // SignedAngle的结果 逆时针为负
                    float anglePreToTarget = -Vector2.SignedAngle(beforeDir, targetDirV2);
                    if (anglePreToTarget < 0)
                    {
                        anglePreToTarget += 360;
                    }

                    if ((changeAngle > 0&&anglePreToTarget < changeAngle)||(changeAngle < 0&&changeAngle+360 < anglePreToTarget))
                    {
                        OwnerTriggerList?.OnTrigger(TriggerType.OnBulletHitTarget,oneEntity);
                    }
                }
            }

            if (_endSumRotateAngle != 0)
            {
                if (Math.Abs(_curSumRotateAngle) >= _endSumRotateAngle)
                {
                    OnDead();
                }
            }
        }

        private void ComputeCurPosAndDir()
        {
            LogicPosition = CenterLogicPosition + new Vector3(distanceFromCenter * Mathf.Cos(Mathf.Deg2Rad*_curAngle), 0, -distanceFromCenter * Mathf.Sin(Mathf.Deg2Rad*_curAngle));
            if (GObj != null)
            {
                GObj.transform.rotation = Quaternion.Euler(0,_curAngle+90,0);
            }
        }
        public override void Clear()
        {
            base.Clear();
            _curAngle = 0;
            distanceFromCenter = 0;
            _endSumRotateAngle = 0;
            _curSumRotateAngle = 0;
            _rotateAngleSpeed = 0;
            StartAngle = 0;
        }
    }
}