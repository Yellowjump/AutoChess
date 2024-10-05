using System;
using System.Collections.Generic;
using System.IO;
using DataTable;
using Entity;
using GameFramework;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class TargetPickerOwnerDirection:TargetPickerBase
    {
        public override TargetPickerType CurTargetPickerType => TargetPickerType.OwnerDirection;
        public bool LengthUseWeapon;
        public TableParamInt WeaponLength;
        public TableParamInt ValidAngle;
        public override List<EntityBase> GetTarget(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.ParentTriggerList != null)
            {
                List<EntityBase> targetList = ListPool<EntityBase>.Get();
                var owner = trigger.ParentTriggerList.Owner;
                if (owner is EntityQizi entityOwner)
                {
                    var target = entityOwner.CurAttackTarget;
                    var lineMidDir = (target.LogicPosition - entityOwner.LogicPosition).ToVector2();//当前朝向//扇形中线
                    var ownerPos2V = entityOwner.LogicPosition.ToVector2();
                    var weaponLength = WeaponLength.Value;
                    if (LengthUseWeapon)
                    {
                        var skill = trigger.ParentTriggerList.ParentSkill;
                        if (skill != null&&skill.FromItemID!=0)
                        {
                            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
                            if (itemTable.HasDataRow(skill.FromItemID))
                            {
                                var itemData = itemTable[skill.FromItemID];
                                var lengthArray = itemData.AssetObjLength;
                                if (lengthArray.Length != 0)
                                {
                                    weaponLength = lengthArray[0];
                                }
                            }
                        }
                    }
                    lineMidDir = lineMidDir.normalized * weaponLength / 1000f;
                    var direnList = GameEntry.HeroManager.DirenList;
                    foreach (var oneEntity in direnList)
                    {
                        if (!oneEntity.IsValid)
                        {
                            continue;
                        }
                        var targetPos2V = oneEntity.LogicPosition.ToVector2();
                        var targetDir2V = (oneEntity.LogicPosition - entityOwner.LogicPosition).ToVector2();
                        if (targetDir2V.magnitude < 0.5f)
                        {
                            targetList.Add(oneEntity);//owner在 target的范围里
                            continue;
                        }
                        //判断圆心是否在扇形内
                        if (IsPointInSector(targetPos2V, ownerPos2V, lineMidDir, weaponLength / 1000f, ValidAngle.Value,0.5f))
                        {
                            targetList.Add(oneEntity);//owner在 target的范围里
                            continue;
                        }
                        // 判断扇形的两条半径线是否与圆碰撞
                        float halfAngleInRad = ValidAngle.Value / 2f;
                        var cosHalfAngle = (float)Math.Cos(halfAngleInRad*Mathf.Deg2Rad);
                        var sinHalfAngle = (float)Math.Sin(halfAngleInRad*Mathf.Deg2Rad);
                        var rline1 = new Vector2 (lineMidDir.x * cosHalfAngle - lineMidDir.y * sinHalfAngle, lineMidDir.x * sinHalfAngle + lineMidDir.y * cosHalfAngle);
                        var rline2 = new Vector2(lineMidDir.x * cosHalfAngle + lineMidDir.y * sinHalfAngle, -lineMidDir.x * sinHalfAngle + lineMidDir.y * cosHalfAngle);
                        // 判断圆是否与两条半径线碰撞
                        if (DistanceToSegment(targetPos2V, ownerPos2V, ownerPos2V + rline1) <= 0.5f ||
                            DistanceToSegment(targetPos2V, ownerPos2V, ownerPos2V + rline2) <= 0.5f)
                        {
                            targetList.Add(oneEntity);
                        }
                    }
                }
                return targetList;
            }
            return null;
        }
        /// <summary>
        /// 判断点p是否在扇形S内
        /// </summary>
        /// <param name="p"></param>
        /// <param name="A">S的圆心</param>
        /// <param name="midDir">扇形的中线</param>
        /// <param name="l">扇形的半径</param>
        /// <param name="a">扇形角度</param>
        /// <param name="r">圆p所在圆的半径</param>
        /// <returns></returns>
        private bool IsPointInSector(Vector2 p, Vector2 A, Vector2 midDir, float l, float a,float r)
        {
            // 计算向量 AP
            Vector2 AP = p - A;

            // 计算AP的长度
            float AP_distSquared = AP.x * AP.x + AP.y * AP.y;

            // 点在扇形的半径范围内或者至少圆和扇形相切
            if (AP_distSquared <= (l+r) * (l+r))
            {
                // 计算向量与扇形中线的夹角
                float dotProduct = Vector2.Dot(AP,midDir) ;
                float AP_magnitude = AP.magnitude;
                float midDir_magnitude = midDir.magnitude;
                float cosTheta = Math.Clamp(dotProduct / (AP_magnitude * midDir_magnitude),-1,1);

                // 将角度限制在扇形范围内
                float halfAngle = a / 2;
                var cosRadin = Math.Acos(cosTheta);
                if (cosRadin <= halfAngle*Mathf.Deg2Rad)
                {
                    return true;
                }
            }
            return false;
        }
        // 计算点到线段的最短距离
        private float DistanceToSegment(Vector2 P, Vector2 A, Vector2 B)
        {
            var distAB = Vector2.Distance(A, B);
            if (distAB == 0) return Vector2.Distance(P, A);
            var t = Vector2.Dot((P-A), (B- A)) / distAB;
            t = Math.Max(0, Math.Min(1, t));
            return Vector2.Distance(P, (A + t * (B - A)));
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            LengthUseWeapon = reader.ReadBoolean();
            WeaponLength.ReadFromFile(reader);
            ValidAngle.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(LengthUseWeapon);
            WeaponLength.WriteToFile(writer);
            ValidAngle.WriteToFile(writer);
        }

        public override void Clone(TargetPickerBase copy)
        {
            if (copy is TargetPickerOwnerDirection TargetPicker)
            {
                TargetPicker.LengthUseWeapon = LengthUseWeapon;
                WeaponLength.Clone(TargetPicker.WeaponLength);
                ValidAngle.Clone(TargetPicker.ValidAngle);
            }
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            WeaponLength.SetSkillValue(dataTable);
            ValidAngle.SetSkillValue(dataTable);
        }

        public override void Clear()
        {
            if (WeaponLength != null)
            {
                ReferencePool.Release(WeaponLength);
                WeaponLength = null;
            }
            if (ValidAngle != null)
            {
                ReferencePool.Release(ValidAngle);
                ValidAngle = null;
            }
        }
    }
}