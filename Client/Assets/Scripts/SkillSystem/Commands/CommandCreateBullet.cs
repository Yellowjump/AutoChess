using System.IO;
using DataTable;
using Entity;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandCreateBullet:CommandBase
    {
        public override CommandType CurCommandType => CommandType.CreateBullet;
        public TableParamInt CurBulletID = new TableParamInt();
        public TriggerList BulletTrigger = new TriggerList();

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            
            var newBullet = QiziGuanLi.instance.CreateBullet(CurBulletID.Value);
            newBullet.Caster = trigger.ParentTriggerList.ParentSkill.Caster;
            newBullet.Target = trigger.CurTarget as EntityQizi;
            newBullet.LogicPosition = newBullet.Caster.LogicPosition;
            if (BulletTrigger != null)
            {
                var newTriggerList = SkillFactory.CreateNewEmptyTriggerList();
                BulletTrigger.Clone(newTriggerList);
                newTriggerList.ParentSkill = trigger.ParentTriggerList.ParentSkill;
                newTriggerList.Owner = newBullet;
                newBullet.OwnerTriggerList = newTriggerList;
            }
            
        }
        public override void Clone(CommandBase copy)
        {
            if (copy is CommandCreateBullet copyCreateBullet)
            {
                CurBulletID.Clone(copyCreateBullet.CurBulletID);
                BulletTrigger.Clone(copyCreateBullet.BulletTrigger);
            }
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CurBulletID.ReadFromFile(reader);
            BulletTrigger.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            CurBulletID.WriteToFile(writer);
            BulletTrigger.WriteToFile(writer);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            CurBulletID.SetSkillValue(dataTable);
            BulletTrigger.SetSkillValue(dataTable);
        }
    }
}