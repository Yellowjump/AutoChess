using System.IO;
using Entity;
using Entity.Bullet;
using GameFramework;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandRemoveBullet:CommandBase
    {
        public override CommandType CurCommandType => CommandType.RemoveBullet;
        public bool RemoveCurBullet;
        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (RemoveCurBullet&&trigger.ParentTriggerList.Owner is BulletBase curBullet)
            {
                curBullet.OnDead();
                return;
            }
            if (trigger != null && trigger.CurTargetList != null && trigger.CurTargetList.Count > 0)
            {
                foreach (var oneTarget in trigger.CurTargetList)
                {
                    if (oneTarget is BulletBase targetBullet)
                    {
                        if (targetBullet.IsValid == false)
                        {
                            continue;
                        }
                        targetBullet.OnDead();
                    }
                    
                }
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(RemoveCurBullet);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            RemoveCurBullet = reader.ReadBoolean();
        }
        public override void Clone(CommandBase copy)
        {
            if (copy is CommandRemoveBullet copyCmd)
            {
                copyCmd.RemoveCurBullet = RemoveCurBullet;
            }
        }
    }
}