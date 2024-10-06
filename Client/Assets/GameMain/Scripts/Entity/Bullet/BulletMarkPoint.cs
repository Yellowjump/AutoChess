using System.Collections.Generic;
using GameFramework;
using SkillSystem;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace Entity.Bullet
{
    public class BulletMarkPoint:BulletBase
    {
        public int AllDuration;//总持续时间ms
        private float _liveDuration;//已存在时间
        public List<Buff> BuffList;
        public override void SetParamValue(List<TableParamInt> paramIntArray)
        {
            BuffList = ListPool<Buff>.Get();
            if (CurBulletData != null)
            {
                AllDuration = CurBulletData.ParamInt1;
            }
        }
        public override void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.LogicUpdate(elapseSeconds,realElapseSeconds);
            UpdateBuffTime(elapseSeconds, realElapseSeconds);
            if (AllDuration != 0)
            {
                _liveDuration += elapseSeconds / 1000f;
                if (_liveDuration >= AllDuration)
                {
                    OnDead();
                }
            }
        }
        private void UpdateBuffTime(float elapseSeconds, float realElapseSeconds)
        {
            List<Buff> tempBuffList = ListPool<Buff>.Get();
            tempBuffList.AddRange(BuffList);
            foreach (var buff in tempBuffList)
            {
                if (buff.IsValid == false)
                {
                    continue;
                }
                buff.RemainMs -= elapseSeconds*1000;
                if (buff.DurationMs!=0&&buff.RemainMs<=0)
                {
                    buff.OnDestory();
                }
                else
                {
                    buff.OnTrigger(TriggerType.PerTick);
                }
            }
            ListPool<Buff>.Release(tempBuffList);
            for (var i = BuffList.Count - 1; i >= 0; i--)
            {
                if (BuffList[i].IsValid == false)
                {
                    ReferencePool.Release(BuffList[i]);
                    BuffList.RemoveAt(i);
                }
            }
        }
        public override void AddBuff(Buff buff)
        {
            BuffList.Add(buff);
            buff.OnActive();
        }

        public override void RemoveBuff(int buffID)
        {
            foreach (var oneBuff in BuffList)
            {
                if (oneBuff.BuffID == buffID)
                {
                    oneBuff.OnDestory();
                }
            }
        }
        public override void OnDead()
        {
            OwnerTriggerList?.OnTrigger(TriggerType.OnDestory);
            List<Buff> tempBuffList = ListPool<Buff>.Get();
            tempBuffList.AddRange(BuffList);
            foreach (var buff in tempBuffList)
            {
                if (buff.IsValid == false)
                {
                    continue;
                }
                buff.OnDestory();
            }
            ListPool<Buff>.Release(tempBuffList);
            IsValid = false;
            GameEntry.HeroManager.DestoryBullet(this);
        }
        public override void Clear()
        {
            AllDuration = 0;
            _liveDuration = 0;
            for (var i = BuffList.Count - 1; i >= 0; i--)
            {
                ReferencePool.Release(BuffList[i]);
            }
            ListPool<Buff>.Release(BuffList);
            base.Clear();
        }
    }
}