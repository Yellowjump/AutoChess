using GameFramework;

namespace Entity.Bullet
{
    public class BulletTracking:BulletBase
    {
        public float MoveSpeed = 10;
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
                OnHitTarget();
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