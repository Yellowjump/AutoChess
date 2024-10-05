using System.Collections.Generic;
using GameFramework;
using SkillSystem;

namespace Entity.Bullet
{
    public class BulletTracking:BulletBase
    {
        public float MoveSpeed = 10;
        public override void SetParamValue(List<TableParamInt> paramIntArray)
        {
            if (CurBulletData != null)
            {
                MoveSpeed = CurBulletData.ParamInt1/1000.0f;
            }
        }
        public override void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.LogicUpdate(elapseSeconds,realElapseSeconds);
            if (Target == null||Target.IsValid==false)
            {
                OnDead();
                return;
            }

            var dir = Target.LogicPosition - LogicPosition;
            if (dir.magnitude < MoveSpeed * elapseSeconds)
            {
                OnHitTarget(Target);//命中目标
                return;
            }
            LogicPosition += dir.normalized * (MoveSpeed * elapseSeconds);
            if (GObj != null)
            {
                GObj.transform.LookAt(Target.LogicPosition);
            }
        }
    }
}