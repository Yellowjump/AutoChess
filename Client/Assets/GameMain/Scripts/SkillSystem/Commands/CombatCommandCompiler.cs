using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using Zizouqi.CombatDots;

namespace SkillSystem
{
    internal static class CombatCommandCompiler
    {
        public static void Compile(ref int handle, List<CommandBase> commands,
            List<CommandBase> managedCommands)
        {
            var nodes = new List<CombatCommandNode>();
            var buffIds = new List<int>();
            managedCommands.Clear();
            foreach (CommandBase command in commands)
            {
                Append(command, nodes, buffIds, managedCommands);
            }
            CombatCommandProgramBridge.Create(ref handle, nodes, buffIds);
        }

        public static void CompileNodes(List<CommandBase> commands,
            List<CombatCommandNode> nodes, List<int> buffIds)
        {
            nodes.Clear();
            buffIds.Clear();
            if (commands == null) return;
            List<CommandBase> temporaryManagedCommands = ListPool<CommandBase>.Get();
            try
            {
                foreach (CommandBase command in commands)
                    Append(command, nodes, buffIds, temporaryManagedCommands);
            }
            finally
            {
                ListPool<CommandBase>.Release(temporaryManagedCommands);
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                CombatCommandNode node = nodes[i];
                node.ManagedIndex = -1;
                nodes[i] = node;
            }
        }

        public static void CollectManagedCommands(List<CommandBase> commands,
            List<CommandBase> managedCommands)
        {
            managedCommands.Clear();
            foreach (CommandBase command in commands)
            {
                managedCommands.Add(command);
                if (command is CommandRepeatExecuteCmd repeat &&
                    repeat.CurCommandList != null)
                {
                    CollectNested(repeat.CurCommandList, managedCommands);
                }
            }
        }

        private static void CollectNested(List<CommandBase> commands,
            List<CommandBase> managedCommands)
        {
            foreach (CommandBase command in commands)
            {
                managedCommands.Add(command);
                if (command is CommandRepeatExecuteCmd repeat &&
                    repeat.CurCommandList != null)
                {
                    CollectNested(repeat.CurCommandList, managedCommands);
                }
            }
        }

        private static void Append(CommandBase command, List<CombatCommandNode> nodes,
            List<int> buffIds, List<CommandBase> managedCommands)
        {
            int index = nodes.Count;
            var node = new CombatCommandNode { ManagedIndex = managedCommands.Count };
            managedCommands.Add(command);
            if (command is CommandRepeatExecuteCmd repeat)
            {
                node.Kind = repeat.CurCommandList == null || repeat.CurCommandList.Count == 0
                    ? CombatCommandKind.NoOp : CombatCommandKind.Repeat;
                node.RepeatNumberType = (int)repeat.CurType;
                node.RepeatParameter = repeat.ParamInt1.Value;
                nodes.Add(node);
                if (node.Kind == CombatCommandKind.Repeat)
                {
                    foreach (CommandBase child in repeat.CurCommandList)
                    {
                        Append(child, nodes, buffIds, managedCommands);
                    }
                }
                node.EndIndex = nodes.Count;
                nodes[index] = node;
                return;
            }
            if (command is CommandCauseDamage damage)
            {
                node.Kind = CombatCommandKind.Damage;
                node.TriggerDamageRelate = damage.TriggerDamageRelate;
                node.DamageMode = (int)damage.CurDamageComputeType;
                node.DamageFixedValue = damage.ParamInt1.Value;
                node.DamageAttributeType = damage.ParamInt2.Value;
                node.DamageAttributePercent = damage.ParamInt3.Value;
            }
            else if (command is CommandChangeAttribute attribute)
            {
                node.Kind = CombatCommandKind.ChangeAttribute;
                node.AttributeType = attribute.TargetAttrType.Value;
                node.Delta = attribute.AddOrReduce ? attribute.ParamInt1.Value : -attribute.ParamInt1.Value;
            }
            else if (command is CommandHuDun shield)
            {
                node.Kind = CombatCommandKind.Shield;
                node.RemoveShield = shield.RemoveCurHudun;
                node.ShieldMode = (int)shield.HudunChangeType;
                node.ShieldFixedValue = shield.ParamInt1.Value;
                node.ShieldAttributeType = shield.ParamInt2.Value;
                node.ShieldAttributePercent = shield.ParamInt3.Value;
            }
            else if (command is CommandRemoveBuff removeBuff)
            {
                node.Kind = CombatCommandKind.RemoveBuff;
                node.RemoveCurrent = removeBuff.RemoveCurBuff;
                node.RemovalId = removeBuff.BuffID.Value;
            }
            else if (command is CommandRemoveBullet removeBullet)
            {
                node.Kind = CombatCommandKind.RemoveProjectile;
                node.RemoveCurrent = removeBullet.RemoveCurBullet;
            }
            else if (command is CommandCreateBuff createBuff)
            {
                node.Kind = CombatCommandKind.CreateBuff;
                if (!createBuff.UseTemplateBuff)
                {
                    node.CreateTemporaryBuff = true;
                }
                else if (createBuff.TemporaryBuff == null)
                {
                    node.BuffIdStart = buffIds.Count;
                    if (createBuff.BuffID.Value != 0)
                    {
                        buffIds.Add(createBuff.BuffID.Value);
                    }
                    if (!string.IsNullOrEmpty(createBuff.BuffList.Value))
                    {
                        foreach (string id in createBuff.BuffList.Value.Split(','))
                        {
                            buffIds.Add(int.Parse(id));
                        }
                    }
                    node.BuffIdCount = buffIds.Count - node.BuffIdStart;
                }
            }
            else if (command is CommandCreateBullet createBullet)
            {
                node.Kind = CombatCommandKind.CreateProjectile;
                node.ProjectileId = createBullet.CurBulletID.Value;
                node.ProjectileParameter = createBullet.ParamInt1.Value;
                var offset = createBullet.PosOffset.Value;
                node.ProjectileOffsetX = offset.x;
                node.ProjectileOffsetY = offset.y;
                node.ProjectileOffsetZ = offset.z;
            }
            else if (command is CommandCreatePosPoint createPoint)
            {
                node.Kind = CombatCommandKind.CreatePositionPoint;
                node.PositionPointMode = (int)createPoint.CurPositionType;
                node.PositionPointDurationMs = createPoint.DurationMS.Value;
                node.PositionPointLookAtTarget = createPoint.LookAtTarget.Value == 1;
                var offset = createPoint.PosOffset.Value;
                node.PositionPointOffsetX = offset.x;
                node.PositionPointOffsetY = offset.y;
                node.PositionPointOffsetZ = offset.z;
                var maximum = createPoint.PosOffsetMax.Value;
                node.PositionPointOffsetMaxX = maximum.x;
                node.PositionPointOffsetMaxY = maximum.y;
                node.PositionPointOffsetMaxZ = maximum.z;
            }
            else if (command is CommandPlayAnim playAnim)
            {
                node.Kind = CombatCommandKind.Presentation;
                node.PresentationKind = CombatPresentationKind.Animation;
                node.PresentationId = playAnim.AnimAssetID.Value;
            }
            else if (command is CommandPlayAudio playAudio)
            {
                node.Kind = CombatCommandKind.Presentation;
                node.PresentationKind = CombatPresentationKind.Audio;
                node.PresentationId = playAudio.AudioID.Value;
                node.PresentationEnable = playAudio.CreateOrRemove;
            }
            else if (command is CommandCreateSfx createSfx)
            {
                node.Kind = CombatCommandKind.Presentation;
                node.PresentationKind = CombatPresentationKind.Sfx;
                node.PresentationId = createSfx.SfxID.Value;
                node.PresentationEnable = createSfx.CreateOrRemove;
            }
            else if (command is CommandShowWeapon showWeapon)
            {
                node.Kind = CombatCommandKind.Presentation;
                node.PresentationKind = CombatPresentationKind.Weapon;
                node.PresentationId = showWeapon.ShowWeaponItemID.Value;
                node.PresentationHandleType = (int)showWeapon.ShowHandleType;
                node.PresentationEnable = showWeapon.ShowOrHidden;
                node.PresentationUseItemWeapon = showWeapon.ShowItemWeapon;
            }
            else
            {
                if (command == null || command.CurCommandType == CommandType.DoNothing)
                {
                    node.Kind = CombatCommandKind.NoOp;
                }
                else
                {
                    throw new NotSupportedException($"Combat command {command.CurCommandType} has no ECS node.");
                }
            }
            node.EndIndex = index + 1;
            nodes.Add(node);
        }
    }
}
