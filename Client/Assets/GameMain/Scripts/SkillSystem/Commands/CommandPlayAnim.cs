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

        internal bool ExecuteTarget(OneTrigger trigger, EntityBase oneTarget,
            int animationId)
        {
            EntityQizi target = oneTarget as EntityQizi;
            if (target == null || target.IsValid == false)
            {
                return false;
            }

            var curSkill = trigger.ParentTriggerList.ParentSkill;
            if (curSkill != null)
            {
                var curCDMs = curSkill.CurCastCDMs;
                var curAnimEndMs = curSkill.DefaultAnimationDurationMs;
                if (curCDMs < curAnimEndMs)//实际CD小于动画时间了，需要动画加速
                {
                    target.AddAnimCommand(animationId, curAnimEndMs / (float)curCDMs);
                    return true;
                }
            }
            target.AddAnimCommand(animationId);
            return false;
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
            HotGameEntry.HeroManager.LoadAnimationClip(AnimAssetID.Value);
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
            HotGameEntry.HeroManager.ReleaseAnimNeed(AnimAssetID.Value);
            // ReferencePool.Release(AnimAssetID);
            // AnimAssetID = null;
        }
    }
}
