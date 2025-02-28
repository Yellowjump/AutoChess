using System.Collections.Generic;
using DataTable;
using GameFramework;
using GameFramework.Fsm;
using SkillSystem;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace Entity
{
    public partial class EntityQizi
    {
        private Dictionary<Skill,List<WeaponEntity>> _weaponDic = new Dictionary<Skill,List<WeaponEntity>>();
        private EntityQiziObjHandle ObjHandle;
        MaterialPropertyBlock propBlock;
        private const string _highLightPropertyName = "_Highlight";
        public void InitObjWeaponHandle()
        {
            ObjHandle = GObj.GetComponent<EntityQiziObjHandle>();
            //init Render
            propBlock = new MaterialPropertyBlock();
            
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

        public void RemoveAllWeapon()
        {
            if (_weaponDic == null )
            {
                return;
            }

            foreach (var oneKeyValue in _weaponDic)
            {
                var list = oneKeyValue.Value;
                if (list != null && list.Count > 0)
                {
                    foreach (var oneWeaponEntity in list)
                    {
                        ReferencePool.Release(oneWeaponEntity);
                    }
                }
                ListPool<WeaponEntity>.Release(list);
            }
            _weaponDic.Clear();
        }

        public void ShowHighlight(bool show)
        {
            if (ObjHandle == null || ObjHandle.EntityRendererList == null||propBlock == null)
            {
                return;
            }

            foreach (var oneRender in ObjHandle.EntityRendererList)
            {
                // 获取当前属性并修改
                oneRender.GetPropertyBlock(propBlock);
                propBlock.SetFloat(_highLightPropertyName, show ? 1 : 0); // 布尔通常用 0/1 表示
                oneRender.SetPropertyBlock(propBlock);
            }
            
        }
    }
}