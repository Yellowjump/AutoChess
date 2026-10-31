using System;
using System.Collections.Generic;
using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine;
using UnityEngine.Pool;
using Unity.Profiling;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    public class CommandCreatePosPoint : CommandBase
    {
        private static readonly ProfilerMarker CreateCommandMarker =
            new ProfilerMarker("Combat.Point.CreateCommand");
        private static readonly ProfilerMarker CloneTriggerMarker =
            new ProfilerMarker("Combat.Point.CloneTrigger");
        private static readonly ProfilerMarker PointEntityMarker =
            new ProfilerMarker("Combat.Point.CreateEntity");
        private static readonly ProfilerMarker PositionMarker =
            new ProfilerMarker("Combat.Point.Position");
        private static readonly ProfilerMarker ActivateMarker =
            new ProfilerMarker("Combat.Point.OnActive");
        private static readonly ProfilerMarker TimerMarker =
            new ProfilerMarker("Combat.Point.ScheduleTimer");
        private static readonly Func<double, double, double> RollOffset =
            (minimum, maximum) => Utility.Random.GetRandomDouble(minimum, maximum);
        public override CommandType CurCommandType => CommandType.CreatePosPoint;
        public PosPointPositionType CurPositionType;
        public TableParamInt LookAtTarget;
        public TableParamInt DurationMS;
        public TableParamVector3 PosOffset;
        public TableParamVector3 PosOffsetMax;
        public TriggerList PosPointTrigger;


        internal void ExecuteTarget(OneTrigger trigger, EntityBase oneTarget,
            CombatCommandNode node)
        {
            if (oneTarget == null || oneTarget.IsValid == false)
            {
                return;
            }
            using var createSample = CreateCommandMarker.Auto();

            EntityPosPoint newPosPoint;
            using (PointEntityMarker.Auto())
                newPosPoint = HotGameEntry.HeroManager.CreatePosPoint();
            newPosPoint.Caster = trigger.ParentTriggerList.ParentSkill.Caster;
            newPosPoint.Target = oneTarget;
            EntityBase owner = trigger.ParentTriggerList.Owner;
            using (PositionMarker.Auto())
            {
                Vector3 position = CombatSpawnPlacement.ResolvePositionPoint(node,
                    owner != null ? owner.LogicPosition : Vector3.zero,
                    oneTarget.LogicPosition, owner != null,
                    (int)oneTarget.BelongCamp, RollOffset);
                Quaternion rotation = Quaternion.identity;
                if (node.PositionPointLookAtTarget)
                {
                    Vector3 direction = oneTarget.LogicHitPosition - position;
                    if (direction.sqrMagnitude > 0.000001f)
                    {
                        rotation = Quaternion.LookRotation(direction);
                    }
                }
                CombatPosPointBridge.Initialize(newPosPoint.CombatHandle,
                    (int)newPosPoint.Caster.BelongCamp,
                    node.PositionPointDurationMs,
                    new Unity.Mathematics.float3(position.x, position.y, position.z),
                    new Unity.Mathematics.quaternion(rotation.x, rotation.y,
                        rotation.z, rotation.w));
            }
            if (PosPointTrigger?.CurTriggerList != null &&
                PosPointTrigger.CurTriggerList.Count != 0)
            {
                bool ecsActive = CombatPointActiveSfxCompiler.TryQueue(
                    PosPointTrigger, newPosPoint.CombatHandle);
                bool ecsTimed = CombatPointTimedTriggerCompiler.TryConfigure(
                    PosPointTrigger, newPosPoint,
                    trigger.ParentTriggerList.ParentSkill);
                bool hasManagedTriggers = false;
                foreach (OneTrigger nested in PosPointTrigger.CurTriggerList)
                {
                    if (ecsActive && nested.CurTriggerType == TriggerType.OnActive)
                        continue;
                    if (ecsTimed && nested.CurTriggerType == TriggerType.PerTick)
                        continue;
                    hasManagedTriggers = true;
                    break;
                }
                if (hasManagedTriggers)
                {
                    var newTriggerList = newPosPoint.OwnerTriggerList ??
                        SkillFactory.CreateNewEmptyTriggerList();
                    using (CloneTriggerMarker.Auto())
                    {
                        PosPointTrigger.ClonePointRuntime(newTriggerList,
                            ecsActive, ecsTimed);
                    }
                    newTriggerList.ParentSkill = trigger.ParentTriggerList.ParentSkill;
                    newTriggerList.Owner = newPosPoint;
                    newPosPoint.OwnerTriggerList = newTriggerList;
                    using (ActivateMarker.Auto())
                    {
                        if (!ecsActive)
                            newTriggerList.OnTrigger(TriggerType.OnActive);
                    }
                    using (TimerMarker.Auto())
                    {
                        if (!ecsTimed && newPosPoint.CombatHandle != 0 &&
                            newTriggerList.TrySchedulePointTimer(newPosPoint.CombatHandle))
                        {
                            CombatPosPointBridge.SetScheduledTick(newPosPoint.CombatHandle);
                        }
                    }
                }
                else if (newPosPoint.OwnerTriggerList != null)
                {
                    ReferencePool.Release(newPosPoint.OwnerTriggerList);
                    newPosPoint.OwnerTriggerList = null;
                }
            }
            else if (newPosPoint.OwnerTriggerList != null)
            {
                ReferencePool.Release(newPosPoint.OwnerTriggerList);
                newPosPoint.OwnerTriggerList = null;
            }
        }

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandCreatePosPoint commandCreatePosPoint)
            {
                commandCreatePosPoint.CurPositionType = CurPositionType;
                LookAtTarget.Clone(commandCreatePosPoint.LookAtTarget);
                DurationMS.Clone(commandCreatePosPoint.DurationMS);
                PosPointTrigger.Clone(commandCreatePosPoint.PosPointTrigger);
                PosOffset.Clone(commandCreatePosPoint.PosOffset);
                PosOffsetMax.Clone(commandCreatePosPoint.PosOffsetMax);
            }
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CurPositionType = (PosPointPositionType)reader.ReadInt32();
            DurationMS.ReadFromFile(reader);
            PosOffset.ReadFromFile(reader);
            PosOffsetMax.ReadFromFile(reader);
            LookAtTarget.ReadFromFile(reader);
            PosPointTrigger.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)CurPositionType);
            DurationMS.WriteToFile(writer);
            PosOffset.WriteToFile(writer);
            PosOffsetMax.WriteToFile(writer);
            LookAtTarget.WriteToFile(writer);
            PosPointTrigger.WriteToFile(writer);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            PosPointTrigger.SetSkillValue(dataTable);
            PosOffset.SetSkillValue(dataTable);
            PosOffsetMax.SetSkillValue(dataTable);
            DurationMS.SetSkillValue(dataTable);
            LookAtTarget.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            ReferencePool.Release(PosPointTrigger);
            PosPointTrigger = null;
            // ReferencePool.Release(DurationMS);
            // ReferencePool.Release(PosOffset);
            // ReferencePool.Release(PosOffsetMax);
            // ReferencePool.Release(LookAtTarget);
            
            // DurationMS = null;
            // PosOffset = null;
            // LookAtTarget = null;
            // PosOffsetMax = null;
        }
    }
}
