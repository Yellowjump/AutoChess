using System.IO;
using DataTable;
using Entity;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public sealed class CommandBurnEffect : CommandBase
    {
        public const int BurnBuffId = 30301;
        private const float DetonationRadius = 2.5f;
        public enum Effect { Ignite, Feed, Detonate, Spread }
        public override CommandType CurCommandType => CommandType.BurnEffect;
        public Effect Mode;
        public TableParamInt Param;

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            var source = trigger.ParentTriggerList.ParentSkill;
            var caster = source.Caster;
            if (caster == null || !caster.IsValid) return;
            if (Mode == Effect.Spread)
            {
                if (arg is EntityQizi origin) Spread(caster, origin, source);
                return;
            }
            if (arg is not CauseDamageData hit || hit.DamageValue <= 0 || hit.BurnDetonated ||
                hit.Target.BelongCamp == caster.BelongCamp || !hit.Target.IsValid ||
                (int)hit.Target.GetAttribute(AttributeType.Hp).GetFinalValue() <= 0) return;

            var target = hit.Target;
            bool active = hit.SourceSkill?.IsActiveDamageSkill == true;
            var burn = target.FindStatusBuff(BuffTag.Burning, BurnBuffId);
            switch (Mode)
            {
                case Effect.Ignite:
                    if (active && hit.SourceSkill.TryIgniteTarget(target.HeroUID)) Apply(caster, target, Param.Value, source);
                    break;
                case Effect.Feed:
                    if (hit.BurnStacksBeforeHit > 0 && burn != null && burn.BurnFeedCooldownMs <= 0)
                    {
                        burn.BurnFeedCooldownMs = Param.Value;
                        Apply(caster, target, 1, source);
                    }
                    break;
                case Effect.Detonate:
                    if (active && burn != null && hit.BurnStacksBeforeHit >= burn.MaxLayerNum)
                    {
                        hit.BurnDetonated = true;
                        burn.OnDestory();
                        int attack = (int)caster.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
                        long damage = (long)attack * hit.BurnStacksBeforeHit * Param.Value / 100;
                        HotGameEntry.HeroManager.BurnVisuals?.Detonate(target.LogicPosition, DetonationRadius);
                        foreach (var enemy in Team(target.BelongCamp))
                            if (enemy.IsValid && (enemy.LogicPosition - target.LogicPosition).sqrMagnitude <=
                                DetonationRadius * DetonationRadius)
                            {
                                CommandCauseDamage.DealSecondaryDamage(enemy, damage, source);
#if UNITY_EDITOR
                                if (HotGameEntry.HeroManager.IsStationaryTargetPreview)
                                    Debug.Log($"[燃烧试招] #{target.HeroUID} 引爆 {hit.BurnStacksBeforeHit} 层，命中 #{enemy.HeroUID}，结算前伤害 {damage}");
#endif
                            }
                        caster.OnTrigger(TriggerType.OnBurnSpread, target);
                    }
                    break;
            }
        }

        private void Spread(EntityQizi caster, EntityQizi origin, Skill source)
        {
            EntityQizi nearest = null;
            float best = float.PositiveInfinity;
            foreach (var enemy in Team(origin.BelongCamp))
            {
                if (enemy == origin || !enemy.IsValid) continue;
                float distance = (enemy.LogicPosition - origin.LogicPosition).sqrMagnitude;
                if (distance >= best) continue;
                nearest = enemy;
                best = distance;
            }
            if (nearest == null) return;
            Apply(caster, nearest, Param.Value, source);
            HotGameEntry.HeroManager.BurnVisuals?.Spread(origin.LogicPosition, nearest.LogicPosition);
#if UNITY_EDITOR
            if (HotGameEntry.HeroManager.IsStationaryTargetPreview)
                Debug.Log($"[燃烧试招] #{origin.HeroUID} 扩散至 #{nearest.HeroUID}，施加 {Param.Value} 层，距离 {Mathf.Sqrt(best):F2}");
#endif
        }

        internal static void SpreadOnDeath(EntityQizi victim)
        {
            var burn = victim.FindStatusBuff(BuffTag.Burning, BurnBuffId);
            if (burn == null) return;
            var opposing = victim.BelongCamp == CampType.Friend ? CampType.Enemy : CampType.Friend;
            foreach (var caster in Team(opposing))
            {
                if (!caster.IsValid || caster.HeroUID != burn.BurnSourceId) continue;
                caster.OnTrigger(TriggerType.OnBurnSpread, victim);
                break;
            }
        }

        private static System.Collections.Generic.List<EntityQizi> Team(CampType camp) =>
            camp == CampType.Friend ? HotGameEntry.HeroManager.QiziCSList : HotGameEntry.HeroManager.DirenList;

#if UNITY_EDITOR
        internal static void FillPreviewBurn(EntityQizi caster, EntityQizi target)
        {
            var row = GameEntry.DataTable.GetDataTable<DRBuff>("Buff")[BurnBuffId];
            Apply(caster, target, row.MaxLayerNum);
            Debug.Log($"[燃烧试招] 主靶 #{target.HeroUID} 补至 {row.MaxLayerNum} 层");
        }
#endif

        private static void Apply(EntityQizi caster, EntityQizi target, int layers, Skill source = null)
        {
            if (layers <= 0) return;
            var row = GameEntry.DataTable.GetDataTable<DRBuff>("Buff")[BurnBuffId];
            var burn = target.FindStatusBuff(BuffTag.Burning, BurnBuffId);
            bool isNew = burn == null;
            if (isNew)
            {
                burn = SkillFactory.CreateNewBuff();
                GameEntry.DataTable.GetDataTable<DRBuffTemplate>("BuffTemplate")[row.TemplateID].BuffTemplate.Clone(burn);
                burn.BuffID = row.Id;
                burn.Owner = target;
                burn.DurationMs = row.Duration;
                burn.MaxLayerNum = row.MaxLayerNum;
                burn.BurnSourceId = caster.HeroUID;
            }
            int attack = (int)caster.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
            int tick = Mathf.Max(1, (int)((long)attack * row.IntParam1 / 100));
            for (int i = 0; i < layers; i++) burn.AddDamageLayer(tick, source);
            if (isNew)
            {
                target.AddBuff(burn);
                if (source != null)
                    HotGameEntry.HeroManager.BurnVisuals?.Ignited(target.LogicPosition);
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write((int)Mode);
            Param.WriteToFile(writer);
        }
        public override void ReadFromFile(BinaryReader reader)
        {
            Mode = (Effect)reader.ReadInt32();
            Param.ReadFromFile(reader);
        }
        public override void SetSkillValue(DataRowBase row) => Param.SetSkillValue(row);
        public override void Clone(CommandBase copy)
        {
            if (copy is not CommandBurnEffect effect) return;
            effect.Mode = Mode;
            Param.Clone(effect.Param);
        }
    }
}
