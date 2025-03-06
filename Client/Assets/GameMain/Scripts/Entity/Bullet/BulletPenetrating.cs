using System.Collections.Generic;
using GameFramework;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace Entity.Bullet
{
    public class BulletPenetrating:BulletBase
    {
        private Vector3 _startPos;
        private Vector3 _targetPos;
        private Vector3 _controlPoint;
        private float _controlPointLerpStartToTarget; // 控制点水平位置
        private float _controlPointOffsetMax; // 最终控制点随机偏移最大值 
        private float _controlPointOffsetMin; // 最终控制点随机偏移最小值 （上下限都是非负数）比如 （500，600）
        private float _controlPointOffsetFinal; // 最终控制点随机偏移 设定line为start到target的距离长度 1000的效果是 让_controlPoint在start和target 连线的中间法线上高度是line/2。_controlPointOffsetFinal为-1000指方向在法线负方向
        private bool _trackingTargetEntity;//追踪目标
        public float MoveSpeed = 10;
        
        private float _traveledDistance = 0f; // 当前已经移动的距离
        private float _totalDistance; // 贝塞尔曲线的总距离
        private Vector3 _previousPosition; // 上一帧的位置，用于判断命中
        private List<Vector3> _curvePoints; // 贝塞尔曲线的采样点
        private List<int> _hasHitQiziUid;
        public override void SetParamValue(List<TableParamInt> paramIntArray)
        {
            if (CurBulletData != null)
            {
                MoveSpeed = CurBulletData.ParamInt1/1000.0f;
                _controlPointOffsetMin = CurBulletData.ParamInt2;
                _controlPointOffsetMax = CurBulletData.ParamInt3;
                _trackingTargetEntity = CurBulletData.ParamInt4==1;
                _controlPointLerpStartToTarget = CurBulletData.ParamInt5;
            }
            // 初始化位置和控制点
            _startPos = LogicPosition;
            _targetPos = Target.LogicHitPosition;
            GenerateRandomControlPoint();

            // 采样贝塞尔曲线并计算总距离
            _curvePoints = SampleBezierCurve(10);
            _totalDistance = CalculateTotalCurveLength(_curvePoints);
            ListPool<Vector3>.Release(_curvePoints);
            _curvePoints = null;
            _previousPosition = _startPos;
            _hasHitQiziUid = ListPool<int>.Get();
        }
        public override void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.LogicUpdate(elapseSeconds,realElapseSeconds);
            // 计算子弹运动的距离
            _traveledDistance += MoveSpeed * elapseSeconds;
            float t = _traveledDistance / _totalDistance;
            t = Mathf.Clamp01(t);

            // 根据贝塞尔曲线计算当前子弹位置
            Vector3 currentPosition = GetBezierPosition(t);
            LogicPosition = currentPosition;
            // 更新子弹方向
            if (GObj != null)
            {
                GObj.transform.LookAt(currentPosition);
            }

            // 判断命中敌人
            var enemyList = GameEntry.HeroManager.GetEnemyList(BelongCamp);
            foreach (var enemy in enemyList)
            {
                if (!enemy.IsValid)
                {
                    continue;
                }
                if (IsHitEnemy(enemy))
                {
                    OnHitTarget(enemy); // 命中目标的逻辑
                }
            }

            // 如果到达目标位置，子弹消失
            if (t >= 1.0f)
            {
                OnDead(); // 子弹消失
            }

            _previousPosition = LogicPosition; // 更新上一帧位置
        }
        public override void OnHitTarget(EntityQizi target)
        {
            OwnerTriggerList?.OnTrigger(TriggerType.OnBulletHitTarget,target);
        }
        // 判断是否命中敌人
        private bool IsHitEnemy(EntityQizi enemy)
        {
            if (_hasHitQiziUid.Contains(enemy.HeroUID))
            {
                return false;
            }
            float hitRadius = 0.5f; // 子弹的命中范围
            if (Vector3.Distance(LogicPosition, enemy.LogicPosition) <= hitRadius)
            {
                _hasHitQiziUid.Add(enemy.HeroUID);
                return true;
            }
            return false;
        }
        // 生成随机控制点
        private void GenerateRandomControlPoint()
        {
            Vector3 midPoint = _startPos + (_targetPos - _startPos) * _controlPointLerpStartToTarget / 1000f;
            float oriDistance = (_targetPos - _startPos).magnitude;
            Vector3 direction = (_targetPos - _startPos).normalized;

            // 计算法线方向并随机选择一个方向
            Vector3 randomOffset = Vector3.Cross(direction, Vector3.up).normalized * oriDistance;
            if (Utility.Random.GetRandomNoLogic(2) == 0)
            {
                randomOffset = -randomOffset;
            }

            // 随机生成控制点偏移量
            _controlPointOffsetFinal = (float)Utility.Random.GetRandomDouble(_controlPointOffsetMin, _controlPointOffsetMax);
            _controlPoint = midPoint + randomOffset * _controlPointOffsetFinal/1000;
        }
        // 采样贝塞尔曲线
        private List<Vector3> SampleBezierCurve(int numSamples)
        {
            List<Vector3> points = ListPool<Vector3>.Get();
            for (int i = 0; i <= numSamples; i++)
            {
                float t = i / (float)numSamples;
                points.Add(GetBezierPosition(t));
            }
            return points;
        }
        // 获取贝塞尔曲线上某点位置
        private Vector3 GetBezierPosition(float t)
        {
            if (_trackingTargetEntity)
            {
                return Mathf.Pow(1 - t, 2) * _startPos + 2 * (1 - t) * t * _controlPoint + Mathf.Pow(t, 2) * Target.LogicHitPosition;
            }
            return Mathf.Pow(1 - t, 2) * _startPos + 2 * (1 - t) * t * _controlPoint + Mathf.Pow(t, 2) * _targetPos;
        }
        // 计算贝塞尔曲线总长度
        private float CalculateTotalCurveLength(List<Vector3> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector3.Distance(points[i - 1], points[i]);
            }
            return length;
        }

        public override void Clear()
        {
            base.Clear();
            if (_hasHitQiziUid != null)
            {
                ListPool<int>.Release(_hasHitQiziUid);
                _hasHitQiziUid = null;
            }
            MoveSpeed = 0;
            _controlPointOffsetMin = 0;
            _controlPointOffsetMax = 0;
            _trackingTargetEntity = false;
            _controlPointLerpStartToTarget = 0;
            _traveledDistance = 0;
            _startPos = Vector3.zero;
            _targetPos = Vector3.zero;
            _previousPosition = Vector3.zero;
        }
    }
}