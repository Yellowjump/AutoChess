using DataTable;
using GameFramework;
using SkillSystem;
using Unity.Profiling;
using Unity.VisualScripting;
using UnityEngine;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace Entity.Bullet
{
    public class BulletBase:EntityBase,IReference
    {
        private static readonly ProfilerMarker TickTriggerMarker =
            new ProfilerMarker("Combat.Bullet.TickTrigger");
        private static readonly ProfilerMarker ActiveTriggerMarker =
            new ProfilerMarker("Combat.Bullet.OnActive");
        protected static readonly ProfilerMarker VisualReadyMarker =
            new ProfilerMarker("Combat.Bullet.VisualReady");

        protected int m_ProjectileHandle;
        internal int ProjectileHandle => m_ProjectileHandle;
        internal virtual void BindRuntime(int handle, int parameter)
        {
            m_ProjectileHandle = handle;
        }
        internal int SpawnOrder;
        public override CampType BelongCamp
        {
            get => m_ProjectileHandle == 0 ? base.BelongCamp :
                (CampType)CombatProjectileBridge.GetCamp(m_ProjectileHandle);
            set
            {
                if (m_ProjectileHandle == 0) base.BelongCamp = value;
                else CombatProjectileBridge.SetCamp(m_ProjectileHandle, (int)value);
            }
        }

        public override bool IsValid
        {
            get => m_ProjectileHandle == 0 ? base.IsValid :
                CombatProjectileBridge.IsAlive(m_ProjectileHandle);
            set
            {
                if (m_ProjectileHandle == 0) base.IsValid = value;
                else CombatProjectileBridge.SetAlive(m_ProjectileHandle, value);
            }
        }

        public override Vector3 LogicPosition
        {
            get
            {
                if (m_ProjectileHandle == 0) return base.LogicPosition;
                CombatProjectileBridge.GetPosition(m_ProjectileHandle,
                    out float x, out float y, out float z);
                return new Vector3(x, y, z);
            }
            set
            {
                if (m_ProjectileHandle == 0) base.LogicPosition = value;
                else
                {
                    CombatProjectileBridge.SetPosition(m_ProjectileHandle, value.x, value.y, value.z);
                }
            }
        }

        public override Vector3 LogicHitPosition => LogicPosition;
        public int BulletID;
        public EntityQizi Caster;//创建者
        public EntityBase Owner;//持有者也就是 子弹发射的地方
        public EntityQizi Target;
        public TriggerList OwnerTriggerList;
        internal Skill RuntimeSourceSkill;
        public DRBullet CurBulletData;
        private HeroComponent.GetGObjSuccessCallback m_GetObjCallback;
        public override void InitGObj()
        {
            m_GetObjCallback ??= OnGetHeroGObjCallback;
            if (OwnerTriggerList != null)
            {
                using (ActiveTriggerMarker.Auto())
                    OwnerTriggerList.OnTrigger(TriggerType.OnActive);
            }
            HotGameEntry.HeroManager.QueueBulletVisual(m_ProjectileHandle,
                SpawnOrder);
        }

        public void BeginVisualLoad()
        {
            HotGameEntry.HeroManager.GetBulletObjByID(BulletID, m_GetObjCallback);
        }
        protected virtual void OnGetHeroGObjCallback(GameObject obj,string path)
        {
            using var visualSample = VisualReadyMarker.Auto();
            GObj = obj;
            GObj.transform.position = LogicPosition;
            GObj.transform.LookAt(Target.LogicHitPosition);
            var trail = GObj.GetComponentInChildren<TrailRenderer>();
            if (trail != null)
            {
                trail.Clear();
            }
        }
        public virtual void UpdateVisual()
        {
            if (GObj != null && m_ProjectileHandle != 0)
                GObj.transform.position = LogicPosition;
        }
        public virtual void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            //各自的飞行逻辑
            if (OwnerTriggerList == null) return;
            using (TickTriggerMarker.Auto())
            {
                OwnerTriggerList.OnTrigger(TriggerType.PerTick);
            }
        }

        public virtual void OnHitTarget(EntityQizi target)
        {
            TriggerHitTarget(target);
            OnDead();
        }

        protected void TriggerHitTarget(EntityQizi target)
        {
            if (!CombatTriggerProjectileSpawnQueue.TryEnqueueSimpleHit(this,
                    target))
                OwnerTriggerList?.OnTrigger(TriggerType.OnBulletHitTarget, target);
        }

        public virtual void OnDead()
        {
            CombatTriggerProjectileSpawnQueue.FlushBeforeRecycle(this);
            OwnerTriggerList?.OnTrigger(TriggerType.OnDestory);
            IsValid = false;
            HotGameEntry.HeroManager.DestoryBullet(this);
        }

        public virtual void Clear()
        {
            CombatProjectileBridge.Release(ref m_ProjectileHandle);
            HotGameEntry.HeroManager.ReleaseBulletGameObject(BulletID,GObj,m_GetObjCallback);
            BulletID = 0;
            SpawnOrder = 0;
            GObj = null;
            IsValid = true;
            Owner = null;
            Caster = null;
            Target = null;
            CurBulletData = null;
            RuntimeSourceSkill = null;
            if (OwnerTriggerList != null)
            {
                OwnerTriggerList.ResetForReuse();
            }
        }
    }
}
