using System.IO;
using Entity;
using GameFramework;
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
            /*var tempAnimString = new TableParamString();
            tempAnimString.ReadFromFile(reader);
            AnimAssetID.CurMatchPropertyIndex = tempAnimString.CurMatchPropertyIndex;
            AnimAssetID.CurMatchTable = tempAnimString.CurMatchTable;*/
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