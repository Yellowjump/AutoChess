using System.Collections.Generic;
using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandCreatePosPoint : CommandBase
    {
        public override CommandType CurCommandType => CommandType.CreatePosPoint;
        public PosPointPositionType CurPositionType;
        public TableParamInt LookAtTarget;
        public TableParamInt DurationMS;
        public TableParamVector3 PosOffset;
        public TableParamVector3 PosOffsetMax;
        public TriggerList PosPointTrigger;

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.CurTargetList != null && trigger.CurTargetList.Count > 0)
            {
                foreach (var oneTarget in trigger.CurTargetList)
                {
                    if (oneTarget == null || oneTarget.IsValid == false)
                    {
                        continue;
                    }

                    var newPosPoint = GameEntry.HeroManager.CreatePosPoint();
                    newPosPoint.Caster = trigger.ParentTriggerList.ParentSkill.Caster;
                    newPosPoint.Target = oneTarget;
                    newPosPoint.BelongCamp = newPosPoint.Caster.BelongCamp;
                    newPosPoint.DurationMs = DurationMS.Value;
                    newPosPoint.RemainMs = DurationMS.Value;
                    newPosPoint.GObj = GameEntry.HeroManager.GetNewEmptyObj(ConstValue.PosPointObjName);
                    var posOffset = PosOffset.Value;
                    if (PosOffsetMax.Value != Vector3.zero)//使用随机位置
                    {
                        var randomX = Utility.Random.GetRandomDouble(PosOffsetMax.Value.x, PosOffset.Value.x);
                        var randomY = Utility.Random.GetRandomDouble(PosOffsetMax.Value.y, PosOffset.Value.y);
                        var randomZ = Utility.Random.GetRandomDouble(PosOffsetMax.Value.z, PosOffset.Value.z);
                        posOffset = new Vector3((float)randomX, (float)randomY, (float)randomZ);
                    }
                    var tempTargetPos = Vector3.zero;
                    switch (CurPositionType)
                    {
                        case PosPointPositionType.OwnerOffset:
                            tempTargetPos = GetPosEntityOffset(trigger.ParentTriggerList.Owner, posOffset, oneTarget);
                            break;
                        case PosPointPositionType.TargetOffset:
                            tempTargetPos = GetPosEntityOffset(oneTarget, posOffset,trigger.ParentTriggerList.Owner);
                            break;
                    }
                    newPosPoint.LogicPosition = tempTargetPos;
                    if (LookAtTarget.Value == 1)
                    {
                        newPosPoint.GObj.transform.LookAt(oneTarget.LogicHitPosition);
                    }
                    if (PosPointTrigger != null)
                    {
                        var newTriggerList = SkillFactory.CreateNewEmptyTriggerList();
                        PosPointTrigger.Clone(newTriggerList);
                        newTriggerList.ParentSkill = trigger.ParentTriggerList.ParentSkill;
                        newTriggerList.Owner = newPosPoint;
                        newPosPoint.OwnerTriggerList = newTriggerList;
                        newTriggerList.OnTrigger(TriggerType.OnActive);
                    }
                }
            }
        }

        private Vector3 GetPosEntityOffset(EntityBase theBase, Vector3 offset, EntityBase theBaseLookAt = null)
        {
            if (theBaseLookAt != null)
            {
                var forward =theBaseLookAt.LogicPosition - theBase.LogicPosition;
                var rotateQ = Quaternion.LookRotation(forward, Vector3.up);
                return theBase.LogicPosition + rotateQ * offset;
            }

            return theBase.LogicPosition + offset;
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
            ReferencePool.Release(DurationMS);
            ReferencePool.Release(PosPointTrigger);
            ReferencePool.Release(PosOffset);
            ReferencePool.Release(PosOffsetMax);
            ReferencePool.Release(LookAtTarget);
            PosPointTrigger = null;
            DurationMS = null;
            PosOffset = null;
            LookAtTarget = null;
            PosOffsetMax = null;
        }
    }
}