using System.Collections.Generic;
using Entity;
using Entity.Bullet;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    // Resolves ECS target identities to current managed objects for synchronous
    // authoring and presentation callbacks during one trigger invocation.
    public readonly struct CombatTargetProjection
    {
        public readonly List<EntityBase> Targets;

        public CombatTargetProjection(List<EntityBase> targets)
        {
            Targets = targets;
        }

        public void AddTarget(int selectionHandle, EntityBase target)
        {
            Targets.Add(target);
            CombatSelectedTarget selected = default;
            switch (target)
            {
                case EntityQizi unit:
                    selected.Kind = CombatSelectedTargetKind.Unit;
                    selected.Handle = unit.CombatUnitHandle;
                    break;
                case BulletBase projectile:
                    selected.Kind = CombatSelectedTargetKind.Projectile;
                    selected.Handle = projectile.ProjectileHandle;
                    break;
                case EntityPosPoint point:
                    selected.Kind = CombatSelectedTargetKind.PositionPoint;
                    selected.Handle = point.CombatHandle;
                    break;
                case null:
                    selected.Kind = CombatSelectedTargetKind.None;
                    break;
                default:
                    selected.Kind = CombatSelectedTargetKind.Managed;
                    break;
            }
            CombatTargetSelectionBridge.Append(selectionHandle, selected);
        }

        public bool ProjectUnitByUid(int heroUid)
        {
            EntityQizi target = HotGameEntry.HeroManager.GetEntityByUID(heroUid);
            if (target == null)
            {
                return false;
            }
            Targets.Add(target);
            return true;
        }

        public static bool HasUnitByUid(int heroUid)
        {
            return HotGameEntry.HeroManager.GetEntityByUID(heroUid) != null;
        }

        public bool ProjectOptionalUnitByUid(int heroUid)
        {
            EntityQizi target = heroUid == 0 ? null :
                HotGameEntry.HeroManager.GetEntityByUID(heroUid);
            return ProjectKnownUnit(target);
        }

        public bool ProjectKnownUnit(EntityQizi target)
        {
            Targets.Add(target);
            return target != null;
        }

        public bool ProjectProjectile(int projectileHandle, int spawnOrder,
            bool pendingTarget)
        {
            BulletBase projectile = HotGameEntry.HeroManager.GetBulletTarget(
                projectileHandle, spawnOrder, pendingTarget);
            if (projectile == null)
            {
                if (pendingTarget || !CombatProjectileBridge.TryGetVisualSource(
                        projectileHandle, spawnOrder, out _)) return false;
                Targets.Add(null);
                return true;
            }
            Targets.Add(projectile);
            return true;
        }

        public void ApplyChoices(int selectionHandle, List<EntityBase> candidates)
        {
            Targets.Clear();
            int count = CombatTargetSelectionBridge.GetChoiceCount(selectionHandle);
            for (int offset = 0; offset < count; offset++)
            {
                Targets.Add(candidates[CombatTargetSelectionBridge.GetChoiceIndex(
                    selectionHandle, offset)]);
            }
        }
    }
}
