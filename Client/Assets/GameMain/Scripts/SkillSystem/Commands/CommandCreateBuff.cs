using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine;
using UnityEngine.Pool;
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

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.CurTargetList != null && trigger.CurTargetList.Count > 0)
            {
                foreach (var oneTarget in trigger.CurTargetList)
                {
                    var target = oneTarget;
                    if (target == null || target.IsValid == false)
                    {
                        continue;
                    }

                    //对当前 target 创建一个buff
                    if (UseTemplateBuff == false)
                    {
                        if (TemporaryBuff != null)
                        {
                            var newBuff = SkillFactory.CreateNewBuff();
                            TemporaryBuff.Clone(newBuff);
                            newBuff.Owner = target;
                            newBuff.ParentSkill = trigger.ParentTriggerList.ParentSkill;
                            target.AddBuff(newBuff);
                        }
                    }
                    else
                    {
                        if (TemporaryBuff == null)
                        {
                            var buffTempTable = GameEntry.DataTable.GetDataTable<DRBuffTemplate>("BuffTemplate");
                            var buffTable = GameEntry.DataTable.GetDataTable<DRBuff>("Buff");
                            var tempBuffIDList = ListPool<int>.Get();
                            if (BuffID.Value != 0)
                            {
                                tempBuffIDList.Add(BuffID.Value);
                            }

                            if (!string.IsNullOrEmpty(BuffList.Value))
                            {
                                tempBuffIDList.AddRange(BuffList.Value.Split(',').Select(int.Parse));
                            }

                            foreach (var oneBuffID in tempBuffIDList)
                            {
                                if (buffTable.HasDataRow(oneBuffID))
                                {
                                    var buffData = buffTable[oneBuffID];
                                    var buffTempId = buffData.TemplateID;
                                    if (buffTempTable != null && buffTempTable.HasDataRow(buffTempId))
                                    {
                                        var buffTemp = buffTempTable[buffTempId].BuffTemplate;
                                        var newBuff = SkillFactory.CreateNewBuff();
                                        buffTemp.Clone(newBuff);
                                        newBuff.BuffID = buffData.Id;
                                        newBuff.ParentSkill = trigger.ParentTriggerList.ParentSkill;
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
                                    Log.Error($"No BuffID:{oneBuffID}");
                                }
                            }
                            ListPool<int>.Release(tempBuffIDList);
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
                    BuffList.Clone(copyCreateBuff.BuffList);
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

            ReferencePool.Release(BuffID);
            BuffID = null;
            ReferencePool.Release(BuffList);
            BuffList = null;
        }
    }
}