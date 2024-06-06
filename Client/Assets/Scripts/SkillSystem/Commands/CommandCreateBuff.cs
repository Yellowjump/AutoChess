using System.IO;
using DataTable;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandCreateBuff:CommandBase
    {
        public override CommandType CurCommandType => CommandType.CreateBuff;
        /// <summary>
        /// 是否创建buff表中的buff
        /// </summary>
        public bool UseTemplateBuff;
        public TableParamInt BuffID = new TableParamInt();
        /// <summary>
        /// <para>UseTemplateBuff 是 false时是临时buff（id为0）</para>
        /// <para>UseTemplateBuff 是 true时是 表格中的buff</para>
        /// <para>但是表示的都是 将要创建的 buff </para>
        /// </summary>
        public Buff TemporaryBuff;

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.CurTarget != null)
            {
                //对当前 target 创建一个buff
                if (UseTemplateBuff==false)
                {
                    if (TemporaryBuff != null)
                    {
                        var newBuff = SkillFactory.CreateNewBuff();
                        TemporaryBuff.Clone(newBuff);
                        newBuff.Caster = trigger.ParentTriggerList.ParentSkill.Caster;
                        newBuff.Owner = trigger.CurTarget;
                        newBuff.OwnTriggerList.Owner = trigger.CurTarget;
                        newBuff.ParentSkill = trigger.ParentTriggerList.ParentSkill;
                        trigger.CurTarget.AddBuff(newBuff);
                    }
                }
                else
                {
                    if (TemporaryBuff == null)
                    {
                        var buffTempTable = GameEntry.DataTable.GetDataTable<DRBuffTemplate>("BuffTemplate");
                        var buffTable = GameEntry.DataTable.GetDataTable<DRBuff>("Buff");
                        if (buffTable.HasDataRow(BuffID.Value))
                        {
                            var buffData = buffTable[BuffID.Value];
                            var buffTempId = buffData.TemplateID;
                            if (buffTempTable != null && buffTempTable.HasDataRow(buffTempId))
                            {
                                var buffTemp = buffTempTable[buffTempId].BuffTemplate;
                                var newBuff = SkillFactory.CreateNewBuff();
                                buffTemp.Clone(newBuff);
                                newBuff.Caster = trigger.ParentTriggerList.ParentSkill.Caster;
                                newBuff.ParentSkill = trigger.ParentTriggerList.ParentSkill;
                                newBuff.SetSkillValue(buffData);
                                newBuff.Owner = trigger.CurTarget;
                                newBuff.OwnTriggerList.Owner = trigger.CurTarget;
                                trigger.CurTarget.AddBuff(newBuff);
                            }
                            else
                            {
                                Log.Error($"No BuffTemplate ID:{buffTempId}");
                            }
                        }
                        else
                        {
                            Log.Error($"No BuffID:{BuffID.Value}");
                        }
                    }
                    
                }
            }
        }
        public override void Clone(CommandBase copy)
        {
            if (copy is CommandCreateBuff copyCreateBuff)
            {
                copyCreateBuff.UseTemplateBuff = UseTemplateBuff;
                if (UseTemplateBuff)
                {
                    BuffID.Clone(copyCreateBuff.BuffID);
                }
                else
                {
                    copyCreateBuff.TemporaryBuff = SkillFactory.CreateNewBuff();
                    TemporaryBuff.Clone(copyCreateBuff.TemporaryBuff);
                }
            }
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            UseTemplateBuff = reader.ReadBoolean();
            if (UseTemplateBuff)
            {
                BuffID.ReadFromFile(reader);
            }
            else
            {
                TemporaryBuff = SkillFactory.CreateNewBuff();
                TemporaryBuff.ReadFromFile(reader);
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(UseTemplateBuff);
            if (UseTemplateBuff)
            {
                BuffID.WriteToFile(writer);
            }
            else
            {
                if (TemporaryBuff != null)
                {
                    TemporaryBuff.WriteToFile(writer);
                }
                else
                {
                    Debug.LogError("Not UseTemplate but No TemporaryBuff");
                }
            }
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            if (UseTemplateBuff)
            {
                BuffID.SetSkillValue(dataTable);
            }
        }
    }
}