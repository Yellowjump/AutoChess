using System.Collections.Generic;
using DataTable;
using GameFramework;
using GameFramework.Fsm;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace Entity
{
    public partial class EntityQizi
    {
        private List<SfxEntity> _sfxList = new List<SfxEntity>();
        private readonly List<CombatUnitSfxEvent> m_SfxPlayback =
            new List<CombatUnitSfxEvent>();
        public override void AddSfx(int sfxID)
        {
            if (m_CombatUnitHandle != 0 && HotGameEntry.HeroManager.InBattle)
            {
                CombatAttributeBridge.QueueSfxEvent(m_CombatUnitHandle,
                    CombatUnitSfxEventKind.Add, sfxID);
                return;
            }
            ApplyAddSfx(sfxID);
        }

        private void ApplyAddSfx(int sfxID)
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
                    oneNewSfx.FollowOwner = sfxData.FollowOwner;
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
                        _sfxList.RemoveAt(index);
                    }
                }
            }
        }
        public override void RemoveSfx(int sfxID)
        {
            if (m_CombatUnitHandle != 0 && HotGameEntry.HeroManager.InBattle)
            {
                CombatAttributeBridge.QueueSfxEvent(m_CombatUnitHandle,
                    CombatUnitSfxEventKind.Remove, sfxID);
                return;
            }
            ApplyRemoveSfx(sfxID);
        }

        private void ApplyRemoveSfx(int sfxID)
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

        private void ReplaySfxEvents()
        {
            if (m_CombatUnitHandle == 0) return;
            CombatAttributeBridge.DrainSfxEvents(m_CombatUnitHandle,
                m_SfxPlayback);
            foreach (CombatUnitSfxEvent playback in m_SfxPlayback)
            {
                switch (playback.Kind)
                {
                    case CombatUnitSfxEventKind.Add:
                        ApplyAddSfx(playback.SfxId);
                        break;
                    case CombatUnitSfxEventKind.Remove:
                        ApplyRemoveSfx(playback.SfxId);
                        break;
                    case CombatUnitSfxEventKind.Tick:
                        UpdateSfxRemainTime(playback.DeltaSeconds,
                            playback.DeltaSeconds);
                        break;
                }
            }
            m_SfxPlayback.Clear();
        }

        private void RemoveAllSfx()
        {
            if (m_CombatUnitHandle != 0)
                CombatAttributeBridge.ClearSfxEvents(m_CombatUnitHandle);
            m_SfxPlayback.Clear();
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
    }
}
