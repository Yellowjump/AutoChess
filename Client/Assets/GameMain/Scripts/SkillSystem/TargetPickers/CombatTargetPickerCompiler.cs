using System;
using System.Collections.Generic;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    internal static class CombatTargetPickerCompiler
    {
        public static void Compile(ref int handle, TargetPickerBase root)
        {
            var nodes = new List<CombatTargetPickerNode>();
            Append(root, nodes);
            CombatTargetPickerProgramBridge.Create(ref handle, nodes);
        }

        public static void CompileNodes(TargetPickerBase root,
            List<CombatTargetPickerNode> nodes)
        {
            nodes.Clear();
            Append(root, nodes);
        }

        private static int Append(TargetPickerBase picker,
            List<CombatTargetPickerNode> nodes)
        {
            int index = nodes.Count;
            var node = new CombatTargetPickerNode
            {
                Kind = (CombatTargetPickerKind)picker.CurTargetPickerType,
                ChildIndex = -1
            };
            switch (picker)
            {
                case TargetPickerNearest nearest:
                    node.TargetCamp = (int)nearest.TargetCamp;
                    node.LengthMm = nearest.WeaponLength.Value;
                    break;
                case TargetPickerOwnerDistance distance:
                    node.TargetCamp = (int)distance.TargetCamp;
                    node.LengthMm = distance.WeaponLength.Value;
                    node.UseWeaponLength = distance.LengthUseWeapon;
                    break;
                case TargetPickerOwnerDirection direction:
                    node.LengthMm = direction.WeaponLength.Value;
                    node.UseWeaponLength = direction.LengthUseWeapon;
                    node.AngleDegrees = direction.ValidAngle.Value;
                    break;
                case TargetPickerRelatedDamageTarget damage:
                    node.DamageTargetType = (int)damage.CurDamageDataTargetType;
                    break;
                case TargetPickerBullet bullet:
                    node.BulletId = bullet.BulletID.Value;
                    node.SameCaster = bullet.SameCaster;
                    break;
                case TargetPickerRandomFromList random:
                    node.NumberType = (int)random.CurNumberType;
                    node.NumberParameter = random.ParamInt1.Value;
                    node.MeetNumber = random.MeetNumber;
                    break;
                case TargetPickerSkillCasterCurTarget _:
                case TargetPickerSkillCaster _:
                case TargetPickerArg _:
                case TargetPickerTriggerOwner _:
                    break;
                default:
                    if (picker.CurTargetPickerType != TargetPickerType.NoTarget)
                    {
                        throw new NotSupportedException($"Combat picker {picker.CurTargetPickerType} has no ECS node.");
                    }
                    break;
            }
            nodes.Add(node);
            if (picker is TargetPickerRandomFromList parent &&
                parent.WorkTargetPicker != null)
            {
                node.ChildIndex = Append(parent.WorkTargetPicker, nodes);
                nodes[index] = node;
            }
            return index;
        }
    }
}
