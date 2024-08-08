using GameFramework;

namespace Entity.Bullet
{
    public class BulletRotateOwner:BulletBase
    {
        
        public float MoveSpeed = 10;
        public override void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            
            if (Target == null)
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
        }
    }
}