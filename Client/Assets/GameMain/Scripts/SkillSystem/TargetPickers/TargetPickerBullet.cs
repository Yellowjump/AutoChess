using System;
using System.Collections.Generic;
using System.IO;
using Entity;
using GameFramework;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TargetPickerBullet:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.Bullet;
        public TableParamInt BulletID;
        public bool SameCaster;
        public override List<EntityBase> GetTarget(OneTrigger trigger,object arg = null)
        {
            List<EntityBase> targetList = ListPool<EntityBase>.Get();
            var bulletList = GameEntry.HeroManager.BulletList;
            foreach (var oneBullet in bulletList)
            {
                if (!oneBullet.IsValid)
                {
                    continue;
                }

                if (oneBullet.BulletID == BulletID.Value)
                {
                    if (!SameCaster||(SameCaster && oneBullet.Caster == trigger.ParentTriggerList.ParentSkill.Caster))
                    {
                        targetList.Add(oneBullet);
                    }
                }
            }
            return targetList;
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            BulletID.ReadFromFile(reader);
            SameCaster = reader.ReadBoolean();
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            BulletID.WriteToFile(writer);
            writer.Write(SameCaster);
        }

        public override void Clone(TargetPickerBase copy)
        {
            if (copy is TargetPickerBullet TargetPicker)
            {
                TargetPicker.SameCaster = SameCaster;
                BulletID.Clone(TargetPicker.BulletID);
            }
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            BulletID.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            if (BulletID != null)
            {
                ReferencePool.Release(BulletID);
                BulletID = null;
            }
            SameCaster = false;
        }
    }
}