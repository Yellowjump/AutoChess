using System.IO;
using DataTable;
using Entity;
using Entity.Bullet;
using GameFramework;
using UnityEngine;
using Unity.Profiling;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    public class CommandCreateBullet : CommandBase
    {
        private static readonly ProfilerMarker CreateCommandMarker =
            new ProfilerMarker("Combat.Bullet.CreateCommand");
        private static readonly ProfilerMarker CloneTriggerMarker =
            new ProfilerMarker("Combat.Bullet.CloneTrigger");
        private static readonly ProfilerMarker InitializeRuntimeMarker =
            new ProfilerMarker("Combat.Bullet.InitializeRuntime");
        private static readonly ProfilerMarker InitializeVisualMarker =
            new ProfilerMarker("Combat.Bullet.InitializeVisual");
        public override CommandType CurCommandType => CommandType.CreateBullet;
        public TableParamInt CurBulletID;
        public TableParamInt ParamInt1;
        public TableParamVector3 PosOffset;
        public TriggerList BulletTrigger;

        internal void ExecuteTarget(OneTrigger trigger, EntityBase oneTarget,
            CombatCommandNode node)
        {
            if (oneTarget == null || !oneTarget.IsValid) return;
            Vector3 position = CombatSpawnPlacement.ResolveProjectile(node,
                trigger.ParentTriggerList.Owner.LogicPosition, oneTarget.LogicPosition);
            ExecutePreparedTarget(trigger, oneTarget, node, position);
        }

        internal void ExecutePreparedTarget(OneTrigger trigger, EntityBase oneTarget,
            CombatCommandNode node, Vector3 position)
        {
            if (oneTarget == null || !oneTarget.IsValid) return;
            ExecutePreparedTarget(trigger.ParentTriggerList.ParentSkill.Caster,
                trigger.ParentTriggerList.Owner,
                trigger.ParentTriggerList.ParentSkill, oneTarget, node, position);
        }

        internal void ExecutePointTarget(EntityPosPoint point, EntityQizi target,
            Skill parentSkill)
        {
            if (point == null || target == null || !target.IsValid ||
                parentSkill == null) return;
            Vector3 offset = PosOffset.Value;
            var node = new CombatCommandNode
            {
                Kind = CombatCommandKind.CreateProjectile,
                ProjectileId = CurBulletID.Value,
                ProjectileParameter = ParamInt1.Value,
                ProjectileOffsetX = offset.x,
                ProjectileOffsetY = offset.y,
                ProjectileOffsetZ = offset.z
            };
            Vector3 position = CombatSpawnPlacement.ResolveProjectile(node,
                point.LogicPosition, target.LogicPosition);
            ExecutePreparedTarget(point.Caster, point, parentSkill, target, node,
                position);
        }

        private void ExecutePreparedTarget(EntityQizi caster, EntityBase owner,
            Skill parentSkill, EntityBase oneTarget, CombatCommandNode node,
            Vector3 position)
        {
            if (oneTarget == null || oneTarget.IsValid == false)
            {
                return;
            }
            using var createSample = CreateCommandMarker.Auto();
            var table = GameEntry.DataTable.GetDataTable<DRBullet>("Bullet");
            if (table == null || !table.HasDataRow(node.ProjectileId)) return;
            DRBullet data = table[node.ProjectileId];
            int spawnOrder = CombatBoardBridge.AllocateProjectileSpawnOrder();
            EntityQizi target = oneTarget as EntityQizi;
            CombatProjectileSpawnRequest request = BuildSpawnRequest(data,
                node.ProjectileId, spawnOrder, caster?.HeroUID ?? 0,
                target?.CombatUnitHandle ?? 0, (int)caster.BelongCamp,
                position, target != null ? target.LogicHitPosition : position,
                node.ProjectileParameter);
            bool ecsHit = CombatBulletSimpleHitCompiler.TryBuild(BulletTrigger,
                out CombatProjectileSimpleHit simpleHit);
            request.HasSimpleHit = ecsHit;
            request.SimpleHit = simpleHit;
            int projectileHandle = 0;
            using (InitializeRuntimeMarker.Auto())
            {
                CombatProjectileBridge.Spawn(ref projectileHandle, request);
            }
            BulletBase newBullet = HotGameEntry.HeroManager.CreateBullet(data,
                spawnOrder);
            newBullet.Caster = caster;
            newBullet.BelongCamp = caster.BelongCamp;
            newBullet.Target = target;
            newBullet.Owner = owner;
            newBullet.LogicPosition = position;
            newBullet.RuntimeSourceSkill = parentSkill;
            using (InitializeRuntimeMarker.Auto())
            {
                newBullet.BindRuntime(projectileHandle, node.ProjectileParameter);
                HotGameEntry.HeroManager.RegisterBulletRuntime(newBullet);
            }
            if (BulletTrigger?.CurTriggerList != null &&
                BulletTrigger.CurTriggerList.Count != 0)
            {
                bool hasManagedTriggers = !ecsHit;
                if (ecsHit)
                {
                    foreach (OneTrigger nested in BulletTrigger.CurTriggerList)
                    {
                        if (nested.CurTriggerType == TriggerType.OnBulletHitTarget ||
                            nested.CurTriggerType == TriggerType.OnActive)
                            continue;
                        hasManagedTriggers = true;
                        break;
                    }
                }
                if (hasManagedTriggers)
                {
                    using (CloneTriggerMarker.Auto())
                    {
                        var newTriggerList = newBullet.OwnerTriggerList ??
                            SkillFactory.CreateNewEmptyTriggerList();
                        if (ecsHit) BulletTrigger.CloneProjectileRuntime(newTriggerList);
                        else BulletTrigger.Clone(newTriggerList);
                        newTriggerList.ParentSkill = parentSkill;
                        newTriggerList.Owner = newBullet;
                        newBullet.OwnerTriggerList = newTriggerList;
                    }
                }
                else if (newBullet.OwnerTriggerList != null)
                {
                    ReferencePool.Release(newBullet.OwnerTriggerList);
                    newBullet.OwnerTriggerList = null;
                }
            }
            else if (newBullet.OwnerTriggerList != null)
            {
                ReferencePool.Release(newBullet.OwnerTriggerList);
                newBullet.OwnerTriggerList = null;
            }
            using (InitializeVisualMarker.Auto())
            {
                newBullet.InitGObj();
            }
        }

        internal static CombatProjectileSpawnRequest BuildSpawnRequest(
            DRBullet data, int bulletId, int spawnOrder, int casterUid,
            int targetUnitHandle, int camp, Vector3 position,
            Vector3 targetPosition, int parameter)
        {
            var request = new CombatProjectileSpawnRequest
            {
                Kind = data == null ? CombatProjectileKind.Tracking :
                    (CombatProjectileKind)data.BulletType,
                SpawnOrder = spawnOrder,
                BulletId = bulletId,
                AssetPathId = data?.AssetPathID ?? 0,
                CasterUid = casterUid,
                TargetUnitHandle = targetUnitHandle,
                Camp = camp,
                Position = new Unity.Mathematics.float3(position.x, position.y,
                    position.z),
                TargetPosition = new Unity.Mathematics.float3(targetPosition.x,
                    targetPosition.y, targetPosition.z),
                Speed = data == null ? 10f : data.ParamInt1 / 1000f,
                DurationMs = data?.ParamInt1 ?? 0,
                OrbitDistance = data == null ? 0f : data.ParamInt1 / 1000f,
                OrbitMinimumRange = data == null ? 0f : data.ParamInt2 / 1000f,
                OrbitMaximumRange = data == null ? 0f : data.ParamInt3 / 1000f,
                OrbitAngleSpeed = data?.ParamInt4 ?? 1,
                OrbitEndAngle = data?.ParamInt5 ?? -1,
                OrbitStartAngle = parameter,
                BezierControlLerp = data?.ParamInt5 ?? 0,
                BezierTrackTarget = data != null && data.ParamInt4 == 1
            };
            if (request.Kind == CombatProjectileKind.Bezier)
            {
                request.BezierReverseOffset =
                    Utility.Random.GetRandomNoLogic(2) == 0;
                request.BezierControlOffset = (float)Utility.Random.GetRandomDouble(
                    data?.ParamInt2 ?? 0, data?.ParamInt3 ?? 0);
            }
            return request;
        }

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandCreateBullet copyCreateBullet)
            {
                CurBulletID.Clone(copyCreateBullet.CurBulletID);
                ParamInt1.Clone(copyCreateBullet.ParamInt1);
                BulletTrigger.Clone(copyCreateBullet.BulletTrigger);
                PosOffset.Clone(copyCreateBullet.PosOffset);
            }
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CurBulletID.ReadFromFile(reader);
            ParamInt1.ReadFromFile(reader);
            PosOffset.ReadFromFile(reader);
            BulletTrigger.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            CurBulletID.WriteToFile(writer);
            ParamInt1.WriteToFile(writer);
            PosOffset.WriteToFile(writer);
            BulletTrigger.WriteToFile(writer);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            CurBulletID.SetSkillValue(dataTable);
            BulletTrigger.SetSkillValue(dataTable);
            PosOffset.SetSkillValue(dataTable);
            ParamInt1.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            ReferencePool.Release(BulletTrigger);
            BulletTrigger = null;
            // ReferencePool.Release(ParamInt1);
            // ReferencePool.Release(CurBulletID);
            // ReferencePool.Release(PosOffset);
            // BulletTrigger = null;
            // ParamInt1 = null;
            // CurBulletID = null;
            // PosOffset = null;
        }
    }
}
