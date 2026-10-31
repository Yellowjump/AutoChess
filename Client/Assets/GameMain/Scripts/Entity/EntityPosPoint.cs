using System.Collections.Generic;
using DataTable;
using GameFramework;
using SkillSystem;
using Unity.Profiling;
using UnityEngine;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace Entity
{
    /// <summary>
    /// 位置点，用来装载 sfx，子弹
    /// </summary>
    public class EntityPosPoint:EntityBase,IReference
    {
        private static readonly ProfilerMarker TickTriggerMarker =
            new ProfilerMarker("Combat.Point.TickTrigger");
        private static readonly ProfilerMarker AdvanceMarker =
            new ProfilerMarker("Combat.Point.Advance");
        private static readonly ProfilerMarker SfxMarker =
            new ProfilerMarker("Combat.Point.Sfx");
        private static readonly ProfilerMarker TimedProjectileCommitMarker =
            new ProfilerMarker("Combat.Point.TimedProjectileCommit");

        private int m_CombatHandle;
        internal int CombatHandle => m_CombatHandle;
        internal void CreateCombatPoint(int spawnOrder)
        {
            CombatPosPointBridge.Create(ref m_CombatHandle, spawnOrder);
        }

        public override CampType BelongCamp
        {
            get => m_CombatHandle == 0 ? base.BelongCamp :
                (CampType)CombatPosPointBridge.GetCamp(m_CombatHandle);
            set
            {
                if (m_CombatHandle == 0) base.BelongCamp = value;
                else CombatPosPointBridge.SetCamp(m_CombatHandle, (int)value);
            }
        }

        public override bool IsValid
        {
            get => m_CombatHandle == 0 ? base.IsValid :
                CombatPosPointBridge.IsAlive(m_CombatHandle);
            set
            {
                if (m_CombatHandle == 0) base.IsValid = value;
                else CombatPosPointBridge.SetAlive(m_CombatHandle, value);
            }
        }

        public override Vector3 LogicPosition
        {
            get
            {
                if (m_CombatHandle == 0) return base.LogicPosition;
                CombatPosPointBridge.GetPosition(m_CombatHandle, out float x, out float y, out float z);
                return new Vector3(x, y, z);
            }
            set
            {
                if (m_CombatHandle == 0) base.LogicPosition = value;
                else
                {
                    CombatPosPointBridge.SetPosition(m_CombatHandle, value.x, value.y, value.z);
                    if (GObj != null) GObj.transform.position = value;
                }
            }
        }

        public override Vector3 LogicHitPosition => LogicPosition;
        public EntityQizi Caster;//创建者
        public EntityBase Target;
        public TriggerList OwnerTriggerList;
        private readonly List<CommandCreateBullet> m_TimedProjectileCommands =
            new List<CommandCreateBullet>();
        private readonly List<int> m_TimedProjectileIntents = new List<int>();
        private int m_TimedProjectileCommandCount;
        private Skill m_TimedProjectileParentSkill;

        internal void ConfigureTimedProjectiles(List<CommandBase> commands,
            Skill parentSkill)
        {
            m_TimedProjectileCommandCount = commands.Count;
            m_TimedProjectileParentSkill = parentSkill;
            for (int i = 0; i < commands.Count; i++)
            {
                if (i == m_TimedProjectileCommands.Count)
                    m_TimedProjectileCommands.Add((CommandCreateBullet)
                        SkillFactory.CreateCommand(CommandType.CreateBullet));
                ((CommandCreateBullet)commands[i]).Clone(m_TimedProjectileCommands[i]);
            }
        }

        internal void CommitTimedProjectiles()
        {
            if (m_TimedProjectileCommandCount == 0) return;
            using var sample = TimedProjectileCommitMarker.Auto();
            int pointHandle = m_CombatHandle;
            m_TimedProjectileIntents.Clear();
            CombatPosPointBridge.CollectTimedProjectileIntents(pointHandle,
                m_TimedProjectileIntents);
            try
            {
                if (Target is not EntityQizi target || !target.IsValid)
                    return;
                for (int i = 0; i < m_TimedProjectileIntents.Count; i++)
                {
                    if (m_CombatHandle != pointHandle) break;
                    int commandIndex = m_TimedProjectileIntents[i];
                    if (commandIndex < 0 ||
                        commandIndex >= m_TimedProjectileCommandCount) continue;
                    m_TimedProjectileCommands[commandIndex].ExecutePointTarget(this,
                        target, m_TimedProjectileParentSkill);
                }
            }
            finally
            {
                m_TimedProjectileIntents.Clear();
            }
        }
        private Quaternion m_VisualRotationBeforeInit = Quaternion.identity;
        // The point's orientation is stored on its ECS entity, not a Transform.
        public Quaternion VisualRotation
        {
            get
            {
                if (m_CombatHandle == 0) return m_VisualRotationBeforeInit;
                Unity.Mathematics.quaternion rotation =
                    CombatPosPointBridge.GetRotation(m_CombatHandle);
                return new Quaternion(rotation.value.x, rotation.value.y,
                    rotation.value.z, rotation.value.w);
            }
            set
            {
                if (m_CombatHandle == 0) m_VisualRotationBeforeInit = value;
                else CombatPosPointBridge.SetRotation(m_CombatHandle,
                    new Unity.Mathematics.quaternion(value.x, value.y,
                        value.z, value.w));
            }
        }
        private int m_DurationBeforeInit;
        private float m_RemainingBeforeInit;
        public int DurationMs
        {
            get => m_CombatHandle == 0 ? m_DurationBeforeInit :
                CombatPosPointBridge.GetDuration(m_CombatHandle);
            set
            {
                if (m_CombatHandle == 0) m_DurationBeforeInit = value;
                else CombatPosPointBridge.SetDuration(m_CombatHandle, value);
            }
        }
        public float RemainMs
        {
            get => m_CombatHandle == 0 ? m_RemainingBeforeInit :
                CombatPosPointBridge.GetRemaining(m_CombatHandle);
            set
            {
                if (m_CombatHandle == 0) m_RemainingBeforeInit = value;
                else CombatPosPointBridge.SetRemaining(m_CombatHandle, value);
            }
        }
        private List<SfxEntity> _sfxList = new List<SfxEntity>();
        public void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            LogicUpdate(elapseSeconds, realElapseSeconds, true);
        }

        public void LogicUpdate(float elapseSeconds, float realElapseSeconds,
            bool tickDue)
        {
            int executingHandle = m_CombatHandle;
            //触发perUpdate
            using (TickTriggerMarker.Auto())
            {
                OwnerTriggerList?.OnPointTick(Target, tickDue);
            }
            if (m_CombatHandle != executingHandle || executingHandle == 0)
            {
                return;
            }
            using (AdvanceMarker.Auto())
            {
                CombatPosPointBridge.Advance(executingHandle, elapseSeconds);
            }
            using (SfxMarker.Auto())
            {
                UpdateSfxRemainTime(elapseSeconds, realElapseSeconds);
            }
        }
        public override void AddSfx(int sfxID)
        {
            var sfxTable = GameEntry.DataTable.GetDataTable<DRSfx>("Sfx");
            if (sfxTable.HasDataRow(sfxID))
            {
                var sfxData = sfxTable[sfxID];
                SfxEntity oneNewSfx = null;
                if (sfxData.IsOnlyOne)
                {
                    oneNewSfx = _sfxList.Find(entity => entity.SfxID == sfxID);
                }

                if (oneNewSfx == null)
                {
                    oneNewSfx = ReferencePool.Acquire<SfxEntity>();
                    oneNewSfx.SfxID = sfxID;
                    oneNewSfx.DurationMs = sfxData.DurationMs;
                    oneNewSfx.Owner = this;
                    oneNewSfx.PosOffset = sfxData.PosOffset;
                    if (sfxData.PosRandom != Vector3.zero)
                    {
                        oneNewSfx.PosOffset +=new Vector3((float)Utility.Random.GetRandomNoLogic(-sfxData.PosRandom.x, sfxData.PosRandom.x),(float)Utility.Random.GetRandomNoLogic(-sfxData.PosRandom.y, sfxData.PosRandom.y),(float)Utility.Random.GetRandomNoLogic(-sfxData.PosRandom.z, sfxData.PosRandom.z))  ;
                    }
                    oneNewSfx.SizeOffset = sfxData.SizeOffset;
                    oneNewSfx.InitGObj();
                    _sfxList.Add(oneNewSfx);
                }
                oneNewSfx.TimePassedMs = 0;//刷新特效存在时间
                oneNewSfx.ExistNum++;
            }
        }

        public void UpdateSfxRemainTime(float elapseSeconds, float realElapseSeconds)
        {
            if (_sfxList != null && _sfxList.Count > 0)
            {
                for (var index = _sfxList.Count-1; index >=0; index--)
                {
                    var oneSfx = _sfxList[index];
                    if (oneSfx.DurationMs == 0)//持续时间无线
                    {
                        continue;
                    }
                    oneSfx.TimePassedMs += elapseSeconds * 1000;
                    if (oneSfx.TimePassedMs >= oneSfx.DurationMs)
                    {
                        ReferencePool.Release(oneSfx);
                        _sfxList.Remove(oneSfx);
                    }
                }
            }
        }
        public override void RemoveSfx(int sfxID)
        {
            if (_sfxList == null || _sfxList.Count == 0)
            {
                return;
            }
            var oneSfx = _sfxList.Find(entity => entity.SfxID == sfxID);
            if (oneSfx == null)
            {
                return;
            }
            oneSfx.ExistNum--;
            if (oneSfx.ExistNum <= 0)
            {
                ReferencePool.Release(oneSfx);
                _sfxList.Remove(oneSfx);
            }
        }

        private void RemoveAllSfx()
        {
            if (_sfxList == null || _sfxList.Count == 0)
            {
                return;
            }
            foreach (var oneSfx in _sfxList)
            {
                ReferencePool.Release(oneSfx);
            }
            _sfxList.Clear();
        }
        public void Clear()
        {
            CombatPosPointBridge.Release(ref m_CombatHandle);
            m_TimedProjectileCommandCount = 0;
            m_TimedProjectileParentSkill = null;
            m_TimedProjectileIntents.Clear();
            IsValid = true;
            Caster = null;
            Target = null;
            m_DurationBeforeInit = 0;
            m_RemainingBeforeInit = 0;
            m_VisualRotationBeforeInit = Quaternion.identity;
            if (GObj != null)
            {
                HotGameEntry.HeroManager.ReleaseEmptyObj(GObj);
                GObj = null;
            }

            RemoveAllSfx();
            if (OwnerTriggerList != null)
            {
                OwnerTriggerList.ResetForReuse();
            }
        }
    }
}
