using System.Collections.Generic;
using DataTable;
using GameFramework;
using GameFramework.Fsm;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace Entity
{
    public partial class EntityQizi
    {
        private Dictionary<Skill,List<WeaponEntity>> _weaponDic = new Dictionary<Skill,List<WeaponEntity>>();
        private EntityQiziObjHandle ObjHandle;
        public void InitObjWeaponHandle()
        {
            ObjHandle = GObj.GetComponent<EntityQiziObjHandle>();
        }
        public void AddOneWeapon(int itemID,WeaponHandleType handleType,Skill skill)
        {
            var oneWeapon = ReferencePool.Acquire<WeaponEntity>();
            if (!_weaponDic.ContainsKey(skill))
            {
                var oneList = ListPool<WeaponEntity>.Get();
                _weaponDic.Add(skill,oneList); 
            }
            var weaponList = _weaponDic[skill];
            weaponList.Add(oneWeapon);
            oneWeapon.Init(this,handleType,itemID);
        }

        public Transform GetWeaponHandle(WeaponHandleType handleType)
        {
            switch (handleType)
            {
                case WeaponHandleType.LeftHand:
                    return ObjHandle?.LeftHandHandle;
                case WeaponHandleType.RightHand:
                    return ObjHandle?.RightHandHandle;
            }
            return null;
        }

        public void RemoveOneSkillAllWeapon(Skill skill)
        {
            if (_weaponDic == null || !_weaponDic.ContainsKey(skill))
            {
                return;
            }

            var list = _weaponDic[skill];
            if (list != null && list.Count > 0)
            {
                foreach (var oneWeaponEntity in list)
                {
                    ReferencePool.Release(oneWeaponEntity);
                }
            }
            ListPool<WeaponEntity>.Release(list);
            _weaponDic.Remove(skill);
        }
    }
}