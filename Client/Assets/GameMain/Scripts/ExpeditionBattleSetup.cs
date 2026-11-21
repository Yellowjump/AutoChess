using System.Collections.Generic;
using System.Linq;
using Entity;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;
using WorldTimeline;

internal static class ExpeditionBattleSetup
{
    private sealed class LinkState
    {
        public ArtifactTrigger Effects;
        public bool Front;
        public int KnotBearerId;
        public float NextOpening;
        public readonly PursuitMark Mark = new PursuitMark();
        public LinkState RelayStarter;
        public int Hits, SwordHits;
        public readonly ArtifactVitality Vitality = new ArtifactVitality();
        public readonly ArtifactBattleOutput Output = new ArtifactBattleOutput();
        public bool Has(ArtifactTrigger effect) => (Effects & effect) != 0;
    }

    public static TimelineBattleSetup Create(WorldState world, IReadOnlyList<ActorState> first,
        IReadOnlyList<ActorState> second, bool visible, bool vanguard = false)
    {
        var sides = new[] { first, second };
        var parties = sides.Select(side => side.Select(a => !a.HasJoinedChosen ? world.ExpeditionFor(a.Id) : null)
            .FirstOrDefault(p => p != null)).ToArray();
        var fighters = new Dictionary<int, EntityQizi>();
        var links = new Dictionary<EntityQizi, LinkState>();
        bool useLinks = parties.Any(p => p?.Equipment.LinksEnabled == true);
        bool openingVolley = world.SwordTraining;
        bool firstTick = true;
        float elapsed = 0, pulse = 3f;
        return new TimelineBattleSetup
        {
            AdjustPosition = (actor, original) =>
            {
                int side = first.Contains(actor) ? 0 : 1;
                int cell = parties[side]?.CellOf(actor.Id) ?? -1;
                if (cell >= 0) return Position(cell, side == 0);
                int slot = 0; while (slot < sides[side].Count && sides[side][slot] != actor) slot++;
                if (parties[side]?.Owns(actor.Id) == true)
                    return new Vector2Int(slot % 7, side == 0 ? 1 : 6); // Preview bench, hidden by planning HUD.
                if (vanguard && parties[side] == null) return new Vector2Int(2 + slot % 2 * 2, side == 1 ? 5 : 2);
                return parties[1 - side] != null ? new Vector2Int(slot % 7, side == 1 ? 7 - slot / 7 : slot / 7) : original;
            },
            Configure = (actor, entity) =>
            {
                fighters.Add(actor.Id, entity);
                if (useLinks)
                {
                    int side = first.Contains(actor) ? 0 : 1;
                    var party = parties[side];
                    int cell = party?.CellOf(actor.Id) ?? -1;
                    var link = new LinkState { Front = cell >= 0 && cell < 3,
                        Effects = cell >= 0 ? party.Equipment.BattleTriggers(actor.Id) : ArtifactTrigger.None };
                    links.Add(entity, link);
                    if (entity.Critical != null)
                    {
                        entity.Critical.Equipment = link.Effects;
                        entity.GetAttribute(AttributeType.CriticalDamage).SetBaseNum(CriticalFighter.DamagePercent(link.Effects));
                    }
                    link.Output.EquippedEffects = link.Effects;
                    if (cell >= 0 && world.SwordTraining && !link.Has(ArtifactTrigger.OpeningSword))
                        link.Output.TrainingSwordCase = Enumerable.Range(0, ExpeditionEquipment.CommonSlots)
                            .Any(slot => party.Equipment.Worn(actor.Id, slot)?.DefinitionId == ExpeditionEquipment.GrowingSwordCase);
                    entity.OnArtifactHit = OnHit;
                    entity.ArtifactOutput = link.Output;
                    if (link.Has(ArtifactTrigger.EchoJade)) entity.BeginEchoCast = link.Output.BeginEchoCast;
                    if (link.Front && world.SupportArtifactsEnabled) link.KnotBearerId = party.KnotBearerFor(cell);
                    if (link.Has(ArtifactTrigger.HeartLamp))
                        entity.OnAllyHealed = (primary, gain) =>
                        {
                            var allies = sides[side].Where(a => party.CellOf(a.Id) >= 0)
                                .Select(a => fighters[a.Id]).Where(u => u.IsValid)
                                .Select(u => (u, u.HeroUID, Hp(u), MaxHp(u)));
                            // The transfer has no healer callback of its own.
                            var target = link.Output.ResolveHeartLamp(primary, gain, allies, (u, amount) => u.ReceiveHealing(amount));
                            if (visible && target != null) HotGameEntry.HeroManager.BurnVisuals?.LanternHeal(target);
                        };
                    if (link.Has(ArtifactTrigger.LastStandCharm))
                        link.Output.CheckLastStandHealth(Hp(entity), MaxHp(entity));
                    if (world.SignatureArtifacts || link.KnotBearerId != 0 || link.Has(ArtifactTrigger.ReprisalSeal) || link.Has(ArtifactTrigger.LastStandCharm))
                        entity.ArtifactHealthLost = Lost;
                    if (world.SignatureArtifacts)
                    {
                        entity.BambooActive = () =>
                        {
                            var bearer = Aura(entity,ArtifactTrigger.Bamboo);
                            if (bearer == null) return false;
                            links[bearer].Output.ActivatedEffects |= ArtifactTrigger.Bamboo;
                            return true;
                        };
                        entity.RedirectHealthLoss = Redirect;
                        entity.ArtifactOverheal = Overheal;
                    }
                }
                int role = world.ExpeditionFor(actor.Id)?.LeaderRole ?? ExpeditionParty.Role(actor.Id);
                if (role is >= 0 and < ExpeditionParty.StageRoles)
                    foreach (var skill in entity.NormalSkillList.Where(skill => skill.IsBasicAttack))
                        skill.SkillRange = role >= 2 ? 6000 : 1100;
                UpdateFocus();
            },
            Tick = delta =>
            {
                if (openingVolley)
                {
                    openingVolley = false;
                    foreach (var entry in links)
                    {
                        var wearer = entry.Key;
                        if (!entry.Value.Has(ArtifactTrigger.OpeningSword) || !Living(wearer)) continue;
                        var enemy = links.Keys.Where(u => u.BelongCamp != wearer.BelongCamp && Living(u))
                            .OrderBy(u => wearer.GetDistanceSquare(u)).ThenBy(u => u.HeroUID).FirstOrDefault();
                        if (enemy == null) continue;
                        FireVolley(wearer,enemy,entry.Value,ArtifactTrigger.OpeningSword);
                    }
                }
                elapsed += delta;
                foreach (var entry in links)
                {
                    var state = entry.Value;
                    int amount = state.Vitality.TakeRepeat(elapsed);
                    if (amount <= 0) continue;
                    state.Output.ActivatedEffects |= ArtifactTrigger.SpringPendant;
                    state.Output.RepeatHealing += entry.Key.ReceiveHealing(amount);
                }
                UpdateFocus();
                pulse -= delta;
                bool opening = firstTick;
                firstTick = false;
                bool regularHeal = pulse <= 0;
                if (!regularHeal && !opening) return;
                if (regularHeal) pulse += 3;
                for (int side = 0; side < 2; side++)
                {
                    var party = parties[side];
                    if (party == null || !fighters.TryGetValue(party.FieldId(3), out var healer) || !healer.IsValid ||
                        !sides[1 - side].Any(actor => fighters[actor.Id].IsValid)) continue;
                    bool preparesShield = world.RainTasselEnabled && Aura(healer, ArtifactTrigger.Tide) != null &&
                        sides[side].Any(actor => links.TryGetValue(fighters[actor.Id], out var link) &&
                            link.Has(ArtifactTrigger.RainTassel) && Living(fighters[actor.Id]) &&
                            fighters[actor.Id].GetDistanceSquare(healer) <= 4.5f * 4.5f);
                    if (!regularHeal && !preparesShield) continue;
                    var target = sides[side].Select(actor => fighters[actor.Id]).Where(unit => Living(unit) &&
                            unit.GetDistanceSquare(healer) <= 4.5f * 4.5f && (Hp(unit) < MaxHp(unit) || Aura(unit,ArtifactTrigger.Tide) != null))
                        .OrderBy(unit => (float)Hp(unit) / MaxHp(unit))
                        .ThenByDescending(unit => preparesShield && Hp(unit) == MaxHp(unit) && links[unit].Has(ArtifactTrigger.RainTassel))
                        .ThenBy(unit => unit.HeroUID).FirstOrDefault();
                    if (target == null) continue;
                    int amount = target.ReceiveHealing(System.Math.Max(1, world.Actors[party.LeaderId].MaxHealth * 18 / 100), healer);
                    EntityQizi.RecordBattleOutput(healer.NormalSkillList.FirstOrDefault(skill => skill.IsBasicAttack), healing: amount);
                    if (visible) HotGameEntry.HeroManager.BurnVisuals?.LanternHeal(target);
                }
            }
        };
        void OnHit(EntityQizi attacker, EntityQizi defender, CauseDamageData hit, int absorbed, bool shieldBroken)
        {
            if (!links.TryGetValue(defender, out var guard)) return;
            bool basic = hit.SourceSkill?.IsBasicAttack == true;
            links.TryGetValue(attacker, out var attack);
            int sealDamage = basic && attack != null && attack.Has(ArtifactTrigger.ReprisalSeal)
                ? attack.Vitality.SpendSealCharge((int)attacker.GetAttribute(AttributeType.AttackDamage).GetFinalValue()) : 0;
            if (shieldBroken && guard.Has(ArtifactTrigger.RainTassel) && Living(attacker))
                FireVolley(defender,attacker,guard,ArtifactTrigger.RainTassel);
            if (shieldBroken && guard.Has(ArtifactTrigger.Quake))
            {
                var battle = HotGameEntry.HeroManager;
                var nearby = battle.GetEnemyList(defender.BelongCamp).Where(enemy => Living(enemy) &&
                    battle.IsInSkillRange(battle.GetCombatDistanceSquare(defender,enemy),1000)).ToArray();
                if (visible) battle.BurnVisuals?.StoneImpact(defender.LogicPosition,1f);
                guard.Output.ResolveQuake(nearby,(int)defender.GetAttribute(AttributeType.AttackDamage).GetFinalValue(),
                    (enemy,power) => Living(enemy) ? CommandCauseDamage.DealSecondaryDamage(enemy,power) : 0);
            }
            if (guard.Has(ArtifactTrigger.FalterMirror) && Living(defender) && Living(attacker))
                CommandWeaknessEffect.OnFalterMirrorHit(attacker, elapsed, guard.Front, guard.Output);
            if (attack == null) return;
            bool alive = defender.IsValid && Hp(defender) > 0;
            if (alive && elapsed >= guard.NextOpening && ((guard.Front && guard.Has(ArtifactTrigger.FrontOpening)) ||
                (absorbed > 0 && guard.Has(ArtifactTrigger.ShieldOpening))))
            {
                guard.NextOpening = elapsed + 3f;
                attack.Mark.Apply(elapsed);
                if (guard.Front && guard.Has(ArtifactTrigger.FrontOpening))
                    guard.Output.ActivatedEffects |= ArtifactTrigger.FrontOpening;
                if (absorbed > 0 && guard.Has(ArtifactTrigger.ShieldOpening))
                    guard.Output.ActivatedEffects |= ArtifactTrigger.ShieldOpening;
                guard.Output.Openings++;
            }
            if (!Living(attacker)) return;
            if (basic && attack.Has(ArtifactTrigger.EchoJade)) attack.Output.ChargeEcho();
            if (!basic)
            {
                if (alive && hit.CombatCast?.Echo != null && hit.SourceSkill?.IsActiveDamageSkill == true)
                {
                    int bonus = hit.CombatCast.Echo.TakePower((int)attacker.GetAttribute(AttributeType.AttackDamage).GetFinalValue());
                    if (bonus > 0)
                    {
                        attack.Output.EchoHits++;
                        attack.Output.EchoDamage += CommandCauseDamage.DealSecondaryDamage(defender, bonus);
                    }
                }
                return;
            }
            var pursuit = alive ? guard.Mark.OnBasicHit(elapsed, attacker.HeroUID, attack.Effects) : PursuitHit.None;
            if (pursuit != PursuitHit.None)
            {
                int bonus = PursuitMark.BonusDamage((int)attacker.GetAttribute(AttributeType.AttackDamage).GetFinalValue());
                int damage = CommandCauseDamage.DealSecondaryDamage(defender, bonus);
                if (pursuit == PursuitHit.Relay)
                {
                    if (guard.RelayStarter != null)
                        guard.RelayStarter.Output.ActivatedEffects |= ArtifactTrigger.RelayClasp;
                    guard.RelayStarter = null;
                    attack.Output.RelayHits++;
                    attack.Output.RelayDamage += damage;
                }
                else
                {
                    guard.RelayStarter = attack.Has(ArtifactTrigger.RelayClasp) ? attack : null;
                    attack.Output.Pursuits++;
                    attack.Output.Damage += damage;
                }
            }
            if (sealDamage > 0 && Living(defender))
            {
                attack.Output.ReprisalHits++;
                attack.Output.ReprisalDamage += CommandCauseDamage.DealSecondaryDamage(defender, sealDamage);
            }
            if (attack.Output.LastStandActive && Living(defender))
            {
                int bonus = attack.Output.TakeLastStandHit((int)attacker.GetAttribute(AttributeType.AttackDamage).GetFinalValue());
                if (bonus > 0) attack.Output.LastStandDamage += CommandCauseDamage.DealSecondaryDamage(defender, bonus);
            }
            if (attack.Has(ArtifactTrigger.AfflictionSeal) && Living(defender))
            {
                int power = attack.Output.AfflictionPower((int)attacker.GetAttribute(AttributeType.AttackDamage).GetFinalValue(),
                    hit.BurnStacksBeforeHit, hit.TargetWasBleeding, hit.TargetWasWeakened);
                if (power > 0) attack.Output.AfflictionDamage += CommandCauseDamage.DealSecondaryDamage(defender, power);
            }
            if (attack.Has(ArtifactTrigger.TinderPouch) && Living(defender))
            {
                int layers = attack.Output.TinderLayersOnBasicHit();
                if (layers > 0) CommandBurnEffect.Apply(attacker, defender, layers, tinder: attack.Output);
            }
            if (attack.Has(ArtifactTrigger.MeridianSpike) && Living(defender))
                CommandBleedEffect.OnMeridianHit(attacker, defender, attack.Output);
            if (attack.Has(ArtifactTrigger.ThunderBell) && Living(defender))
                CommandLightningEffect.OnThunderBellHit(attacker, defender, attack.Output);
            if (attack.Has(ArtifactTrigger.SwordCase) && ++attack.SwordHits % 4 == 0)
            {
                var aim = Living(defender) ? defender : links.Keys.FirstOrDefault(u => u.BelongCamp != attacker.BelongCamp && Living(u));
                if (aim != null)
                {
                    FireVolley(attacker,aim,attack,ArtifactTrigger.SwordCase);
                }
            }
            if (!attack.Has(ArtifactTrigger.Remedy) || ++attack.Hits < 3) return;
            attack.Hits = 0;
            var target = links.Keys.Where(unit => unit.BelongCamp == attacker.BelongCamp && unit.IsValid &&
                    Hp(unit) > 0 && (Hp(unit) < MaxHp(unit) || Aura(unit,ArtifactTrigger.Tide) != null))
                .OrderBy(unit => (double)Hp(unit) / MaxHp(unit)).ThenBy(unit => unit.HeroUID).FirstOrDefault();
            if (target == null) return;
            int amount = System.Math.Max(1, (int)((long)MaxHp(target) * 6 / 100));
            var patient = links[target];
            int total = patient.Has(ArtifactTrigger.Recovery) ? 2 * amount : amount;
            int actual = target.ReceiveHealing(total, attacker);
            attack.Output.Remedies++; attack.Output.Healing += System.Math.Min(amount,actual);
            if (actual > amount) { patient.Output.Recoveries++; patient.Output.BonusHealing += actual-amount; }
            patient.Vitality.OnRemedy(total,elapsed);
            if (visible) HotGameEntry.HeroManager.BurnVisuals?.LanternHeal(target);
        }

        bool Living(EntityQizi unit) => unit.IsValid && Hp(unit) > 0;
        EntityQizi Aura(EntityQizi target,ArtifactTrigger effect) =>
            links.Where(p => p.Key.BelongCamp == target.BelongCamp && p.Value.Has(effect) && Living(p.Key))
                .Select(p => p.Key).FirstOrDefault();
        int Redirect(EntityQizi target,int amount)
        {
            var guard = Aura(target,ArtifactTrigger.Sand);
            if (guard == null || guard == target) return amount;
            int share = ExpeditionEquipment.SharedLoss(amount,Hp(guard));
            if (share == 0) return amount;
            int paid = CommandCauseDamage.DealSecondaryDamage(guard,share,sharedLoss:true);
            links[guard].Output.SharedDamage += paid;
            return amount-paid;
        }
        void Lost(EntityQizi target,int before,int after)
        {
            var link = links[target];
            int max = MaxHp(target);
            if (link.KnotBearerId != 0 && fighters.TryGetValue(link.KnotBearerId, out var bearer) && Living(bearer) && Living(target))
            {
                var supporter = links[bearer];
                int shield = supporter.Output.TryKnotShield(before, after, max, MaxHp(bearer));
                if (shield > 0) Shield(target,12003,shield,shield,supporter);
            }
            if (link.Has(ArtifactTrigger.LastStandCharm)) link.Output.CheckLastStandHealth(after, max);
            int attack = link.Has(ArtifactTrigger.ReprisalSeal) ? (int)target.GetAttribute(AttributeType.AttackDamage).GetFinalValue() : 0;
            int amount = link.Vitality.LoseHealth(link.Effects,before,after,max,attack);
            if (amount > 0) Shield(target,12001,amount,2*System.Math.Max(1,(int)(8L*max/100)),link);
        }
        void Overheal(EntityQizi target,int overflow)
        {
            var tide = Aura(target,ArtifactTrigger.Tide);
            if (tide != null) Shield(target,12002,overflow/2,MaxHp(target)/10,links[tide]);
        }
        void Shield(EntityQizi target,int id,int amount,int cap,LinkState source)
        {
            if (amount <= 0 || cap <= 0) return;
            if (id == 12001) source.Output.ActivatedEffects |= ArtifactTrigger.MountainRing;
            if (id == 12002) source.Output.ActivatedEffects |= ArtifactTrigger.Tide;
            var shield = target.FindStatusBuff(BuffTag.Shield,id);
            if (shield == null)
            {
                shield = SkillFactory.CreateNewBuff();
                shield.Owner = target; shield.BuffID = id; shield.OwnBuffTag = BuffTag.Shield;
                shield.ArtifactShield = true; shield.DurationMs = 4000;
                shield.paramInt = 0;
                if (id == 12002 && links[target].Output.HasRainTassel)
                {
                    shield.TideReport = links[target].Output;
                    shield.TideReport.TideShields++;
                }
                target.AddBuff(shield);
            }
            int gain = System.Math.Min(cap-shield.paramInt,amount);
            shield.RemainMs = 4000;
            shield.paramInt += gain;
            target.GetAttribute(AttributeType.HuDun).AddNum(gain);
            source.Output.GrantedShield += gain;
        }
        void FireVolley(EntityQizi attacker,EntityQizi target,LinkState link,ArtifactTrigger source)
        {
            link.Output.ActivatedEffects |= source;
            int count = attacker.BambooActive?.Invoke() == true ? 2 : 1;
            if (source == ArtifactTrigger.RainTassel)
            {
                link.Output.RainVolleys++;
                link.Output.RainSwords += count;
            }
            for (int i = 0; i < count; i++) LaunchSword(attacker,target,link,source);
        }
        void LaunchSword(EntityQizi attacker,EntityQizi target,LinkState link,ArtifactTrigger source)
        {
            var sword = HotGameEntry.HeroManager.CreateBullet(6,attacker.HasSwordRecall);
            sword.Owner = sword.Caster = attacker; sword.Target = target; sword.BelongCamp = attacker.BelongCamp;
            sword.LogicPosition = attacker.LogicHitPosition;
            int damage = (int)attacker.GetAttribute(AttributeType.AttackDamage).GetFinalValue();
            sword.ArtifactSwordHit = enemy =>
            {
                int dealt = CommandCauseDamage.DealSecondaryDamage(enemy,damage);
                if (source == ArtifactTrigger.RainTassel) link.Output.RainDamage += dealt;
                else if (source == ArtifactTrigger.OpeningSword) link.Output.OpeningSwordDamage += dealt;
                else link.Output.SwordDamage += dealt;
            };
            sword.SetParamValue(null);
            sword.InitGObj();
        }

        void UpdateFocus()
        {
            for (int side = 0; side < 2; side++)
            {
                var party = parties[side]; if (party == null) continue;
                fighters.TryGetValue(party.FieldId(0), out var defender);
                foreach (var actor in sides[1 - side])
                    if (fighters.TryGetValue(actor.Id, out var enemy))
                        enemy.PriorityAttackTarget = defender is { IsValid: true } && enemy.IsValid &&
                            enemy.GetDistanceSquare(defender) <= (party.VanguardEnabled ? 2.25f : 9f) ? defender : null;
            }
        }
    }
    public static Vector2Int Position(int cell, bool first) => new Vector2Int(1 + cell % 3 * 2,
        first ? (cell < 3 ? 2 : 0) : (cell < 3 ? 5 : 7));
    private static int Hp(EntityQizi unit) => (int)unit.GetAttribute(AttributeType.Hp).GetFinalValue();
    private static int MaxHp(EntityQizi unit) => (int)unit.GetAttribute(AttributeType.MaxHp).GetFinalValue();
}
