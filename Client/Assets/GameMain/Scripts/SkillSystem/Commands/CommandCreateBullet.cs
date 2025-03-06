using System.Collections.Generic;
using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandCreateBullet : CommandBase
    {
        public override CommandType CurCommandType => CommandType.CreateBullet;
        public TableParamInt CurBulletID;
        public TableParamInt ParamInt1;
        public TableParamVector3 PosOffset;
        public TriggerList BulletTrigger;
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
                    var newBullet = GameEntry.HeroManager.CreateBullet(CurBulletID.Value);
                    newBullet.Caster = trigger.ParentTriggerList.ParentSkill.Caster;
                    newBullet.BelongCamp = newBullet.Caster.BelongCamp;
                    newBullet.Target = oneTarget as EntityQizi;
                    newBullet.Owner = trigger.ParentTriggerList.Owner;
                    var forward = oneTarget.LogicPosition - newBullet.Owner.LogicPosition;
                    var rotateQ = Quaternion.LookRotation(forward, Vector3.up);
                    newBullet.LogicPosition = newBullet.Owner.LogicPosition + rotateQ * PosOffset.Value;
                    if (BulletTrigger != null)
                    {
                        var newTriggerList = SkillFactory.CreateNewEmptyTriggerList();
                        BulletTrigger.Clone(newTriggerList);
                        newTriggerList.ParentSkill = trigger.ParentTriggerList.ParentSkill;
                        newTriggerList.Owner = newBullet;
                        newBullet.OwnerTriggerList = newTriggerList;
                    }
                    List<TableParamInt> paramIntArray = ListPool<TableParamInt>.Get();
                    paramIntArray.Add(ParamInt1);
                    newBullet.SetParamValue(paramIntArray);
                    ListPool<TableParamInt>.Release(paramIntArray);
                    newBullet.InitGObj();
                }
            }
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
            ReferencePool.Release(ParamInt1);
            ReferencePool.Release(CurBulletID);
            ReferencePool.Release(BulletTrigger);
            ReferencePool.Release(PosOffset);
            BulletTrigger = null;
            ParamInt1 = null;
            CurBulletID = null;
            PosOffset = null;
        }
    }
}