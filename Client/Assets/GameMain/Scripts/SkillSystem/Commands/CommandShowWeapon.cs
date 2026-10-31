using System.IO;
using Entity;
using GameFramework;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandShowWeapon:CommandBase
    {
        public override CommandType CurCommandType => CommandType.ShowWeapon;
        public bool ShowOrHidden;
        public bool ShowItemWeapon;
        public WeaponHandleType ShowHandleType;
        public TableParamInt ShowWeaponItemID;

        internal void ExecuteTarget(Skill skill, EntityBase oneTarget, bool show,
            bool useItemWeapon, WeaponHandleType handleType, int weaponItemId)
        {
            EntityQizi target = oneTarget as EntityQizi;
            if (skill == null || target == null || target.IsValid == false)
            {
                return;
            }
            if (show)
            {
                int itemID = useItemWeapon ? skill.FromItemID : weaponItemId;
                if (itemID != 0)
                {
                    target.AddOneWeapon(itemID, handleType, skill);
                }
            }
            else
            {
                target.RemoveOneSkillAllWeapon(skill);
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(ShowOrHidden);
            writer.Write(ShowItemWeapon);
            writer.Write((int)ShowHandleType);
            ShowWeaponItemID.WriteToFile(writer);
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            ShowOrHidden = reader.ReadBoolean();
            ShowItemWeapon = reader.ReadBoolean();
            ShowHandleType = (WeaponHandleType)reader.ReadInt32();
            ShowWeaponItemID.ReadFromFile(reader);
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            ShowWeaponItemID.SetSkillValue(dataTable);
        }

        public override void Clone(CommandBase copy)
        {
            if (copy is CommandShowWeapon copyShowWeapon)
            {
                copyShowWeapon.ShowOrHidden = ShowOrHidden;
                copyShowWeapon.ShowItemWeapon = ShowItemWeapon;
                copyShowWeapon.ShowHandleType = ShowHandleType;
                ShowWeaponItemID.Clone(copyShowWeapon.ShowWeaponItemID);
            }
        }

        public override void Clear()
        {
            // ReferencePool.Release(ShowWeaponItemID);
            // ShowWeaponItemID = null;
        }
    }
}
