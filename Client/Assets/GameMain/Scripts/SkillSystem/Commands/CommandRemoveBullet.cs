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