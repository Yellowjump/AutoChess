using System;
using System.Collections.Generic;
using Entity;
using Entity.Bullet;
using GameFramework;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    internal sealed class CombatCommandCallbacks : ICombatCommandCallbacks, IDisposable, IReference
    {
        private OneTrigger m_Trigger;
        private CombatTargetProjection m_Projection;
        private List<CommandBase> m_Commands;

        public CombatCommandCallbacks()
        {
        }

        public void Bind(OneTrigger trigger,
            CombatTargetProjection projection, List<CommandBase> compiledCommands)
        {
            m_Trigger = trigger;
            m_Projection = projection;
            m_Commands = ListPool<CommandBase>.Get();
            m_Commands.AddRange(compiledCommands);
        }

        public int CountSubItem(int subItemId)
        {
            return CommandRepeatExecuteCmd.CountSubItem(
                m_Trigger.ParentTriggerList.ParentSkill.FromItemID, subItemId);
        }

        public int CurrentBuffHandle()
        {
            return m_Trigger.ParentTriggerList is Buff buff && buff.Owner is EntityQizi
                ? buff.TimerHandle : 0;
        }

        public int DamageCasterHandle(int commandIndex)
        {
            return ((CommandCauseDamage)m_Commands[commandIndex])
                .GetDamageCaster()?.CombatUnitHandle ?? 0;
        }

        public void ExecuteDamage(int commandIndex, int targetIndex, float damage,
            bool triggerDamageRelate)
        {
            CommandCauseDamage command = (CommandCauseDamage)m_Commands[commandIndex];
            command.ApplyDamage(m_Trigger,
                (EntityQizi)m_Projection.Targets[targetIndex],
                command.GetDamageCaster(), damage, triggerDamageRelate);
        }

        public bool RemoveCurrent(int commandIndex)
        {
            if (m_Commands[commandIndex] is CommandRemoveBuff &&
                m_Trigger.ParentTriggerList is Buff currentBuff)
            {
                currentBuff.OnDestory();
                return true;
            }
            if (m_Commands[commandIndex] is CommandRemoveBullet &&
                m_Trigger.ParentTriggerList.Owner is BulletBase currentBullet)
            {
                currentBullet.OnDead();
                return true;
            }
            return false;
        }

        public void RemoveTarget(int commandIndex, int targetIndex,
            CombatSelectedTarget selected, int buffId)
        {
            EntityBase target = m_Projection.Targets[targetIndex];
            if (m_Commands[commandIndex] is CommandRemoveBuff)
            {
                if (target != null && target.IsValid)
                {
                    target.RemoveBuff(buffId);
                }
                return;
            }
            if (selected.Kind != CombatSelectedTargetKind.Projectile ||
                selected.Handle == 0) return;
            BulletBase bullet = target as BulletBase ??
                HotGameEntry.HeroManager.GetBulletTarget(selected.Handle, 0, false);
            if (bullet != null && bullet.IsValid)
                bullet.OnDead();
            else
            {
                int handle = selected.Handle;
                CombatProjectileBridge.Release(ref handle);
            }
        }

        public void CreateTarget(int commandIndex, int targetIndex,
            CombatSelectedTarget selected, int buffId,
            CombatCommandNode node)
        {
            EntityBase target = m_Projection.Targets[targetIndex];
            switch (m_Commands[commandIndex])
            {
                case CommandCreateBuff createBuff:
                    createBuff.ExecuteTarget(m_Trigger, target, buffId,
                        node.CreateTemporaryBuff);
                    break;
                case CommandCreateBullet createBullet:
                    createBullet.ExecuteTarget(m_Trigger, target, node);
                    break;
                case CommandCreatePosPoint createPoint:
                    createPoint.ExecuteTarget(m_Trigger, target, node);
                    break;
            }
        }

        public bool PresentTarget(int commandIndex, int targetIndex,
            CombatSelectedTarget selected,
            CombatCommandNode node)
        {
            EntityBase target = m_Projection.Targets[targetIndex];
            switch (node.PresentationKind)
            {
                case CombatPresentationKind.Animation:
                    return ((CommandPlayAnim)m_Commands[commandIndex]).ExecuteTarget(
                        m_Trigger, target, node.PresentationId);
                case CombatPresentationKind.Audio:
                    ((CommandPlayAudio)m_Commands[commandIndex]).ExecuteTarget(
                        m_Trigger, target, node.PresentationEnable, node.PresentationId);
                    break;
                case CombatPresentationKind.Sfx:
                    ((CommandCreateSfx)m_Commands[commandIndex]).ExecuteTarget(
                        target, node.PresentationEnable, node.PresentationId);
                    break;
                case CombatPresentationKind.Weapon:
                    ((CommandShowWeapon)m_Commands[commandIndex]).ExecuteTarget(
                        m_Trigger.ParentTriggerList.ParentSkill, target,
                        node.PresentationEnable,
                        node.PresentationUseItemWeapon,
                        (WeaponHandleType)node.PresentationHandleType,
                        node.PresentationId);
                    break;
            }
            return false;
        }

        public void Dispose()
        {
            ReferencePool.Release(this);
        }

        public void Clear()
        {
            if (m_Commands != null)
            {
                ListPool<CommandBase>.Release(m_Commands);
                m_Commands = null;
            }
            m_Trigger = null;
            m_Projection = default;
        }
    }
}
