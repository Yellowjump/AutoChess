using System.Collections.Generic;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Entity.Bullet
{
    public sealed class BulletReturningSword : BulletBase
    {
        private const float Overrun = 2.25f;
        private const float HitRadius = .5f;
        private const float TurnPause = .14f;
        private readonly HashSet<int> hitThisLeg = new HashSet<int>();
        private Vector3 origin;
        private Vector3 turnaround;
        private float speed;
        private float turnRemaining;
        private bool returning;

        public override void SetParamValue(List<TableParamInt> paramIntArray)
        {
            origin = LogicPosition;
            Vector3 aim = Target.LogicHitPosition;
            Vector3 direction = aim - origin;
            direction.y = 0f;
            turnaround = aim + direction.normalized * Overrun;
            speed = CurBulletData.ParamInt1 / 1000f;
        }

        public override void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.LogicUpdate(elapseSeconds, realElapseSeconds);
            if (turnRemaining > 0f)
            {
                turnRemaining -= elapseSeconds;
                if (turnRemaining >= 0f) return;
                elapseSeconds = -turnRemaining;
                turnRemaining = 0f;
            }

            Vector3 destination = returning ? origin : turnaround;
            Vector3 from = LogicPosition;
            Vector3 direction = destination - from;
            float distance = speed * elapseSeconds;
            bool arrived = direction.sqrMagnitude <= distance * distance;
            LogicPosition = arrived ? destination : from + direction.normalized * distance;
            if (GObj != null && direction.sqrMagnitude > .0001f)
                GObj.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            foreach (var enemy in HotGameEntry.HeroManager.GetEnemyList(BelongCamp))
            {
                if (enemy.IsValid && TouchesSegment(enemy.LogicHitPosition, from, LogicPosition) &&
                    hitThisLeg.Add(enemy.HeroUID))
                    OwnerTriggerList?.OnTrigger(TriggerType.OnBulletHitTarget, enemy);
            }

            if (!arrived) return;
            if (returning)
            {
                OnDead();
                return;
            }

            returning = true;
            turnRemaining = TurnPause;
            hitThisLeg.Clear();
            HotGameEntry.HeroManager.BurnVisuals?.SwordTurn(turnaround);
        }

        private static bool TouchesSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector2 offset = new Vector2(point.x - start.x, point.z - start.z);
            Vector2 segment = new Vector2(end.x - start.x, end.z - start.z);
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(offset, segment) / lengthSquared) : 0f;
            return (offset - segment * t).sqrMagnitude <= HitRadius * HitRadius;
        }

        public override void Clear()
        {
            base.Clear();
            hitThisLeg.Clear();
            returning = false;
            turnRemaining = 0f;
        }
    }
}
