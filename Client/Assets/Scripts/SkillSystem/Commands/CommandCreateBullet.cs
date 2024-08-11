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
        public TableParamInt ParamInt1 = new TableParamInt();
        public TriggerList BulletTrigger = new TriggerList();

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            
            var newBullet = GameEntry.HeroManager.CreateBullet(CurBulletID.Value);
            newBullet.Caster = trigger.ParentTriggerList.ParentSkill.Caster;
            newBullet.BelongCamp = newBullet.Caster.BelongCamp;
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
            newBullet.SetParamValue(new[]{ParamInt1});
            newBullet.InitGObj();
        }
        public override void Clone(CommandBase copy)
        {
            if (copy is CommandCreateBullet copyCreateBullet)
            {
                CurBulletID.Clone(copyCreateBullet.CurBulletID);
                ParamInt1.Clone(copyCreateBullet.ParamInt1);
                BulletTrigger.Clone(copyCreateBullet.BulletTrigger);
            }
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            CurBulletID.ReadFromFile(reader);
            ParamInt1.ReadFromFile(reader);
            BulletTrigger.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            CurBulletID.WriteToFile(writer);
            ParamInt1.WriteToFile(writer);
            BulletTrigger.WriteToFile(writer);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            CurBulletID.SetSkillValue(dataTable);
            BulletTrigger.SetSkillValue(dataTable);
        }
    }
}