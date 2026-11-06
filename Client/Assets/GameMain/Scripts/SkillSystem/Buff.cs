using System;
using System.Collections.Generic;
using System.IO;
using Entity;
using UnityEngine.Pool;
using UnityGameFramework.Runtime;

namespace SkillSystem
{

    public class Buff:TriggerList
    {
        public string EditorDesc = string.Empty;
        public int BuffID;
        public int TempleteID;
        public BuffTag OwnBuffTag;
        public int MaxLayerNum;
        public bool FreshOtherBuffDuration;
        public bool IsAura => MaxLayerNum < 0;
        public int DurationMs;//总持续时间，无限是0
        public float RemainMs;//剩余时间
        public bool IsValid = true;
        public int StackCount { get; private set; } = 1;
        internal bool CanMergeStacks => BuffID > 0 && MaxLayerNum > 0
            && OwnBuffTag == BuffTag.None && CurTriggerList.Count == 0;
        private List<float> layerTimers;
        private struct DamageLayer
        {
            public float ElapsedMs;
            public int PaidTicks;
            public int DamagePerTick;
        }

        private List<DamageLayer> damageLayers;
        public bool HasDamageLayers => damageLayers != null;
        public float BurnFeedCooldownMs { get; set; }
        public int BurnSourceId { get; set; }

        public void AddDamageLayer(int damagePerTick)
        {
            damageLayers ??= ListPool<DamageLayer>.Get();
            if (MaxLayerNum > 0 && damageLayers.Count >= MaxLayerNum) return;
            damageLayers.Add(new DamageLayer { DamagePerTick = damagePerTick });
            StackCount = damageLayers.Count;
        }

        public void TickDamageLayers(float elapsedMs)
        {
            BurnFeedCooldownMs = Math.Max(0, BurnFeedCooldownMs - elapsedMs);
            long damage = 0;
            int retained = 0;
            for (int i = 0; i < damageLayers.Count; i++)
            {
                var layer = damageLayers[i];
                layer.ElapsedMs = Math.Min(DurationMs, layer.ElapsedMs + elapsedMs);
                int dueTicks = (int)(layer.ElapsedMs / 1000f);
                damage += (long)(dueTicks - layer.PaidTicks) * layer.DamagePerTick;
                layer.PaidTicks = dueTicks;
                if (layer.ElapsedMs < DurationMs) damageLayers[retained++] = layer;
            }
            damageLayers.RemoveRange(retained, damageLayers.Count - retained);
            StackCount = retained;
            if (damage > 0 && Owner is EntityQizi target && target.IsValid)
                CommandCauseDamage.DealSecondaryDamage(target, damage);
            if (retained == 0) OnDestory();
        }

        internal void AddMarkLayer()
        {
            if (DurationMs == 0 || FreshOtherBuffDuration || MaxLayerNum == 1)
            {
                StackCount = Math.Min(StackCount + 1, MaxLayerNum);
                RemainMs = DurationMs;
                return;
            }

            if (layerTimers == null)
            {
                layerTimers = ListPool<float>.Get();
                layerTimers.Add(RemainMs);
            }
            // Timers are ordered by application time. At the cap, replace the oldest layer.
            if (layerTimers.Count == MaxLayerNum) layerTimers.RemoveAt(0);
            layerTimers.Add(DurationMs);
            StackCount = layerTimers.Count;
            RemainMs = DurationMs;
        }

        internal void AdvanceDuration(float elapsedMs)
        {
            RemainMs -= elapsedMs;
            if (layerTimers != null)
            {
                for (int i = 0; i < layerTimers.Count; i++) layerTimers[i] -= elapsedMs;
                while (layerTimers.Count > 0 && layerTimers[0] <= 0f) layerTimers.RemoveAt(0);
                StackCount = layerTimers.Count;
            }
            if (DurationMs != 0 && RemainMs <= 0f) OnDestory();
        }
        public override void Clone(TriggerList copy)
        {
            if (copy is Buff copyBuff)
            {
                copyBuff.TempleteID = TempleteID;
                copyBuff.EditorDesc = EditorDesc;
                copyBuff.OwnBuffTag = OwnBuffTag;
            }
            base.Clone(copy);
        }
        public override void ReadFromFile(BinaryReader reader)
        {
            TempleteID = reader.ReadInt32();
            EditorDesc = reader.ReadString();
            OwnBuffTag = GetCombinedTags(reader.ReadInt32());
            base.ReadFromFile(reader);
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(TempleteID);
            writer.Write(EditorDesc);
            writer.Write((int)OwnBuffTag);
            base.WriteToFile(writer);
        }
        private BuffTag GetCombinedTags(int combinedValue)
        {
            BuffTag result = BuffTag.None;
            foreach (BuffTag tag in Enum.GetValues(typeof(BuffTag)))
            {
                if ((combinedValue & (int)tag) != 0)
                {
                    result |= tag;
                }
            }
            return result;
        }

        public bool CheckBuffTag(BuffTag checkTag)
        {
            return (checkTag & OwnBuffTag) == checkTag;
        }
        public override void OnDestory()
        {
            if (IsValid == false)
            {
                return;
            }
            IsValid = false;
            base.OnDestory();
            if (IsAura)
            {
                if (Owner is EntityQizi qizi)
                {
                    qizi.OnAuraBuffDestroy(this);
                }
            }
        }

        public override void Clear()
        {
            BuffID = 0;
            TempleteID = 0;
            OwnBuffTag = BuffTag.None;;
            MaxLayerNum = 0;
            FreshOtherBuffDuration = false;
            DurationMs = 0;
            RemainMs = 0;
            IsValid = true;
            StackCount = 1;
            BurnFeedCooldownMs = 0;
            BurnSourceId = 0;
            if (damageLayers != null)
            {
                ListPool<DamageLayer>.Release(damageLayers);
                damageLayers = null;
            }
            if (layerTimers != null)
            {
                ListPool<float>.Release(layerTimers);
                layerTimers = null;
            }
            base.Clear();
        }
    }
}
