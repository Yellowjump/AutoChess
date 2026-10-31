using System.Collections.Generic;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    internal sealed class CombatTargetPickerCallbacks : ICombatTargetPickerCallbacks, IReference
    {
        private OneTrigger m_Trigger;
        private CombatTargetProjection m_Projection;
        private object m_Argument;
        private EntityQizi m_ProjectileCaster;

        public CombatTargetPickerCallbacks()
        {
        }

        public void Bind(OneTrigger trigger, CombatTargetProjection projection,
            object argument)
        {
            m_Trigger = trigger;
            m_Projection = projection;
            m_Argument = argument;
        }

        public void Clear()
        {
            m_Trigger = null;
            m_Projection = default;
            m_Argument = null;
            m_ProjectileCaster = null;
        }

        public void AppendCaster(int selectionHandle)
        {
            EntityQizi caster = m_Trigger?.ParentTriggerList?.ParentSkill?.Caster;
            if (caster != null)
            {
                m_Projection.AddTarget(selectionHandle, caster);
            }
        }

        public void AppendArgument(int selectionHandle)
        {
            if (m_Argument is EntityQizi unit)
            {
                m_Projection.AddTarget(selectionHandle, unit);
            }
        }

        public void AppendOwner(int selectionHandle)
        {
            if (m_Trigger?.ParentTriggerList != null)
            {
                m_Projection.AddTarget(selectionHandle, m_Trigger.ParentTriggerList.Owner);
            }
        }

        public void AppendCasterAttackTarget(int selectionHandle)
        {
            EntityQizi caster = m_Trigger?.ParentTriggerList?.ParentSkill?.Caster;
            if (caster != null)
            {
                m_Projection.AddTarget(selectionHandle, caster.CurAttackTarget);
            }
        }

        public bool TryGetCasterUnit(out int unitHandle)
        {
            EntityQizi caster = m_Trigger?.ParentTriggerList?.ParentSkill?.Caster;
            unitHandle = caster == null ? 0 : caster.CombatUnitHandle;
            return caster != null;
        }

        public bool TryGetOwner(bool includePosition, out CombatPickerSource source)
        {
            source = default;
            if (m_Trigger?.ParentTriggerList == null)
            {
                return false;
            }
            EntityBase owner = m_Trigger.ParentTriggerList.Owner;
            if (owner == null)
            {
                return false;
            }
            if (owner is EntityQizi unit)
            {
                source.IsUnit = true;
                source.UnitHandle = unit.CombatUnitHandle;
                if (!includePosition)
                {
                    return true;
                }
            }
            Vector3 position = owner.LogicPosition;
            source.X = position.x;
            source.Z = position.z;
            source.Camp = (int)owner.BelongCamp;
            return true;
        }

        public bool TryGetDamage(out int casterUid, out int targetUid)
        {
            casterUid = 0;
            targetUid = 0;
            if (!(m_Argument is CauseDamageData damage))
            {
                return false;
            }
            casterUid = damage.Caster == null ? 0 : damage.Caster.HeroUID;
            targetUid = damage.Target == null ? 0 : damage.Target.HeroUID;
            return true;
        }

        public bool ProjectDamageSource(int source)
        {
            CauseDamageData damage = (CauseDamageData)m_Argument;
            return m_Projection.ProjectKnownUnit(source == 0
                ? damage.Caster : damage.Target);
        }

        public int GetParentItemId()
        {
            return m_Trigger.ParentTriggerList.ParentSkill?.FromItemID ?? 0;
        }

        public int GetArgItemId(out bool hasArgSkill)
        {
            hasArgSkill = m_Argument is Skill;
            return hasArgSkill ? ((Skill)m_Argument).FromItemID : 0;
        }

        public int ResolveProjectileCasterUid(bool sameCaster)
        {
            m_ProjectileCaster = sameCaster
                ? m_Trigger.ParentTriggerList.ParentSkill.Caster : null;
            return m_ProjectileCaster == null ? 0 : m_ProjectileCaster.HeroUID;
        }

        public void CollectPendingProjectiles(int bulletId, bool sameCaster,
            List<CombatProjectileSelection.PendingProjectile> pending)
        {
            HotGameEntry.HeroManager.CollectPendingBulletTargets(
                bulletId, sameCaster, m_ProjectileCaster, pending);
        }

        public bool ProjectUnit(int heroUid) => m_Projection.ProjectUnitByUid(heroUid);
        public bool HasManagedUnit(int heroUid) =>
            CombatTargetProjection.HasUnitByUid(heroUid);
        public bool ProjectOptionalUnit(int heroUid) =>
            m_Projection.ProjectOptionalUnitByUid(heroUid);
        public bool ProjectProjectile(int handle, int spawnOrder, bool pending) =>
            m_Projection.ProjectProjectile(handle, spawnOrder, pending);
        public int LookupWeaponLength(int itemId, int fallbackMm) =>
            TargetPickerBase.LookupWeaponLength(itemId, fallbackMm);
        public int RollIndex(int maxExclusive) =>
            Utility.Random.GetRandom(maxExclusive);

        public int CountSubItem(int itemId, int subItemId)
        {
            var table = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            if (table.HasDataRow(itemId))
            {
                foreach (var entry in table[itemId].CraftList)
                {
                    if (entry.Item1 == subItemId)
                    {
                        return entry.Item2;
                    }
                }
            }
            return 0;
        }

        public object CaptureTargets()
        {
            List<EntityBase> targets = ListPool<EntityBase>.Get();
            targets.AddRange(m_Projection.Targets);
            return targets;
        }

        public void ApplyChoices(int selectionHandle, object captured)
        {
            m_Projection.ApplyChoices(selectionHandle, (List<EntityBase>)captured);
        }

        public void ClearTargets() => m_Projection.Targets.Clear();
        public void ReleaseTargets(object captured) =>
            ListPool<EntityBase>.Release((List<EntityBase>)captured);
    }
}
