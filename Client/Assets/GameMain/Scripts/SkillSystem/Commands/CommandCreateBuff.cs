using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandCreateBuff : CommandBase
    {
        public override CommandType CurCommandType => CommandType.CreateBuff;

        /// <summary>
        /// 是否创建buff表中的buff
        /// </summary>
        public bool UseTemplateBuff;

        public TableParamInt BuffID;
        public TableParamString BuffList;
        /// <summary>
        /// <para>UseTemplateBuff 是 false时是临时buff（id为0）</para>
        /// <para>UseTemplateBuff 是 true时是 表格中的buff</para>
        /// <para>但是表示的都是 将要创建的 buff </para>
        /// </summary>
        public Buff TemporaryBuff;


        internal void ExecuteTarget(OneTrigger trigger, EntityBase target, int buffId,
            bool createTemporaryBuff)
        {
            ExecuteTarget(trigger.ParentTriggerList.ParentSkill, target,
                TemporaryBuff, buffId, createTemporaryBuff);
        }

        internal static void ExecuteTarget(Skill sourceSkill, EntityBase target,
            Buff temporaryBuff, int buffId, bool createTemporaryBuff)
        {
            if (target == null || target.IsValid == false)
            {
                return;
            }

            //对当前 target 创建一个buff
            if (createTemporaryBuff)
            {
                if (temporaryBuff != null)
                {
                    var newBuff = SkillFactory.CreateNewBuff();
                    temporaryBuff.Clone(newBuff);
                    newBuff.Owner = target;
                    newBuff.ParentSkill = sourceSkill;
                    newBuff.DurationMs = temporaryBuff.DurationMs;
                    newBuff.RemainMs = newBuff.DurationMs;
                    target.AddBuff(newBuff);
                }
            }
            else
            {
                var buffTempTable = GameEntry.DataTable.GetDataTable<DRBuffTemplate>("BuffTemplate");
                var buffTable = GameEntry.DataTable.GetDataTable<DRBuff>("Buff");
                if (buffTable.HasDataRow(buffId))
                {
                    var buffData = buffTable[buffId];
                    var buffTempId = buffData.TemplateID;
                    if (buffTempTable != null && buffTempTable.HasDataRow(buffTempId))
                    {
                        var buffTemp = buffTempTable[buffTempId].BuffTemplate;
                        var newBuff = SkillFactory.CreateNewBuff();
                        buffTemp.Clone(newBuff);
                        newBuff.BuffID = buffData.Id;
                        newBuff.ParentSkill = sourceSkill;
                        newBuff.DurationMs = buffData.Duration;
                        newBuff.RemainMs = buffData.Duration;
                        newBuff.MaxLayerNum = buffData.MaxLayerNum;
                        newBuff.FreshOtherBuffDuration = buffData.FreshDuration;
                        newBuff.SetSkillValue(buffData);
                        newBuff.Owner = target;
                        target.AddBuff(newBuff);
                    }
                    else
                    {
                        Log.Error($"No BuffTemplate ID:{buffTempId}");
                    }
                }
                else
                {
                    Log.Error($"No BuffID:{buffId}");
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
                    if (copyCreateBuff.TemporaryBuff != null)
                    {
                        ReferencePool.Release(copyCreateBuff.TemporaryBuff);
                        copyCreateBuff.TemporaryBuff = null;
                    }
                    BuffID.Clone(copyCreateBuff.BuffID);
                    BuffList.Clone(copyCreateBuff.BuffList);
                }
                else
                {
                    copyCreateBuff.TemporaryBuff ??= SkillFactory.CreateNewBuff();
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
                BuffList.ReadFromFile(reader);
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
                BuffList.WriteToFile(writer);
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
                BuffList.SetSkillValue(dataTable);
            }
            else
            {
                if (TemporaryBuff != null)
                {
                    TemporaryBuff.SetSkillValue(dataTable);
                }
            }
        }

        public override void Clear()
        {
            if (TemporaryBuff != null)
            {
                ReferencePool.Release(TemporaryBuff);
                TemporaryBuff = null;
            }

            // ReferencePool.Release(BuffID);
            // BuffID = null;
            // ReferencePool.Release(BuffList);
            // BuffList = null;
        }
    }
}
