using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandPlayAnim:CommandBase
    {
        public override CommandType CurCommandType => CommandType.PlayAnim;
        public TableParamInt AnimAssetID;
        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.CurTargetList != null && trigger.CurTargetList.Count > 0)
            {
                foreach (var oneTarget in trigger.CurTargetList)
                {
                    EntityQizi target = oneTarget as EntityQizi;
                    if (target == null || target.IsValid == false)
                    {
                        continue;
                    }

                    if (AnimAssetID.CurMatchTable == GenerateEnumDataTables.Skill && AnimAssetID.CurMatchPropertyIndex == (int)DRSkillField.SkillAnim)
                    {
                        var curSkill = trigger.ParentTriggerList.ParentSkill;
                        if (curSkill != null)
                        {
                            var cdr =  (int)target.GetAttribute(AttributeType.CooldownReduce).GetFinalValue();
                            var reducePercent = cdr / (cdr + 100f);
                            var curCDMs = Mathf.CeilToInt(curSkill.DefaultSkillCDMs * (1 - reducePercent));
                            var curAnimEndMs = curSkill.DefaultAnimationDurationMs;
                            if (curCDMs < curAnimEndMs)//实际CD小于动画时间了，需要动画加速
                            {
                                target.AddAnimCommand(AnimAssetID.Value,curAnimEndMs/(float)curCDMs);
                                return;
                            }
                        }
                    }
                    target.AddAnimCommand(AnimAssetID.Value);
                }
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            AnimAssetID.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            AnimAssetID.ReadFromFile(reader);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            AnimAssetID.SetSkillValue(dataTable);
        }

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandPlayAnim copyAnim)
            {
                AnimAssetID.Clone(copyAnim.AnimAssetID);
            }
        }

        public override void Clear()
        {
            ReferencePool.Release(AnimAssetID);
            AnimAssetID = null;
        }
    }
}