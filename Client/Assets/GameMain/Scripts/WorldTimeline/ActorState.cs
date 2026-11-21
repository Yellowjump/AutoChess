using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public readonly struct SkillBattleOutput
    {
        public readonly int SkillId, ItemId;
        public readonly long Damage, Shield, Healing, ShieldAbsorbed;

        public SkillBattleOutput(int skillId, int itemId, long damage, long shield, long healing, long shieldAbsorbed = 0)
        {
            SkillId = skillId; ItemId = itemId;
            Damage = damage; Shield = shield; Healing = healing;
            ShieldAbsorbed = shieldAbsorbed;
        }
    }

    // All impacts spawned by the same charged cast share this single use.
    public sealed class EchoCast
    {
        private bool spent;
        public int TakePower(int attack)
        {
            if (spent) return 0;
            spent = true;
            return (int)((long)attack * 80 / 100);
        }
    }

    public sealed class CriticalFighter
    {
        public const int BaseChance = 5, BaseDamage = 150;
        public ArtifactTrigger Equipment;
        public int PearlBonus { get; private set; }
        public bool PearlCharged { get; private set; }
        public int CriticalHits, MarksApplied, MarksUsed;
        private float nextMirror, markExpires;
        private CriticalFighter markedBy;

        public static int DamagePercent(ArtifactTrigger equipment) =>
            BaseDamage + ((equipment & ArtifactTrigger.JadeRendBlade) != 0 ? 40 : 0);

        internal void RecordBasic(bool critical)
        {
            if ((Equipment & ArtifactTrigger.HiddenEdgePearl) != 0)
            {
                PearlCharged |= !critical;
                PearlBonus = critical ? 0 : Math.Min(100, PearlBonus + 10);
            }
        }

        public bool HasMark(float now) => markedBy != null && now < markExpires;

        public bool TryMark(CriticalFighter target, float now)
        {
            if ((Equipment & ArtifactTrigger.BreachMirror) == 0 || now < nextMirror || target.HasMark(now))
                return false;
            target.markedBy = this;
            target.markExpires = now + 3f;
            nextMirror = now + 8f;
            MarksApplied++;
            return true;
        }

        internal bool ConsumeAllyMark(CriticalFighter attacker, float now)
        {
            if (!HasMark(now) || markedBy == attacker) return false;
            markedBy.MarksUsed++;
            markedBy = null;
            return true;
        }
    }

    // Every impact from this action, including delayed or returning projectiles, carries the same cast.
    public sealed class CombatCast
    {
        public readonly EchoCast Echo;
        private readonly CriticalFighter attacker;
        private readonly bool basic;
        private bool? naturalCritical;
        private HashSet<CriticalFighter> guaranteedTargets;

        public CombatCast(CriticalFighter attacker, bool basic, EchoCast echo)
        {
            this.attacker = attacker;
            this.basic = basic;
            Echo = echo;
        }

        public bool IsCriticalHit(CriticalFighter target, float now, int chance, Func<int> roll, out bool opened)
        {
            opened = false;
            if (attacker == null) return false;
            if (!naturalCritical.HasValue)
            {
                naturalCritical = roll() < Math.Min(100, chance + (basic ? attacker.PearlBonus : 0));
                if (basic) attacker.RecordBasic(naturalCritical.Value);
            }
            if (!basic && target != null && guaranteedTargets?.Contains(target) != true &&
                target.ConsumeAllyMark(attacker, now))
            {
                (guaranteedTargets ??= new HashSet<CriticalFighter>()).Add(target);
                opened = true;
            }
            return naturalCritical.Value || (target != null && guaranteedTargets?.Contains(target) == true);
        }
    }

    public sealed class ArtifactBattleOutput
    {
        public ArtifactTrigger EquippedEffects;
        public bool TrainingSwordCase;
        // Some effects can activate without dealing damage or restoring health.
        public ArtifactTrigger ActivatedEffects;
        public int Openings, Pursuits, Remedies, Recoveries;
        public int RelayHits;
        public long RelayDamage;
        public int ReprisalHits;
        public long ReprisalDamage;
        public int LampTransfers;
        public long LampHealing;

        private int tinderHits;
        public int TinderLayers;
        public long TinderDamage;
        public int MeridianLayers, MeridianBursts;
        public long MeridianTickDamage, MeridianBurstDamage;

        private int thunderBellHits;
        public int ThunderBellJumps;
        public long ThunderBellDamage;

        private float nextMirrorTime;
        public int MirrorWeakens, MirrorOpenings;

        public bool LastStandActive { get; private set; }
        public int LastStandHits { get; private set; }
        public long LastStandDamage;

        public int DoubleAfflictionHits, TripleAfflictionHits;
        public long AfflictionDamage;

        private int echoCharge;
        public int EchoHits;
        public long EchoDamage;
        public int KnotShield { get; private set; }

        public void ChargeEcho() => echoCharge = Math.Min(2, echoCharge + 1);

        public EchoCast BeginEchoCast()
        {
            if (echoCharge < 2) return null;
            echoCharge = 0;
            return new EchoCast();
        }

        public int TryKnotShield(int before, int after, int maximum, int bearerMaximum)
        {
            if (KnotShield > 0 || after <= 0 || before * 2L <= maximum || after * 2L > maximum) return 0;
            KnotShield = Math.Max(1, (int)((long)bearerMaximum * 12 / 100));
            return KnotShield;
        }

        public int AfflictionPower(int attack, int burnLayers, bool bleeding, bool weakened)
        {
            int kinds = (burnLayers > 0 ? 1 : 0) + (bleeding ? 1 : 0) + (weakened ? 1 : 0);
            switch (kinds)
            {
                case 2:
                    DoubleAfflictionHits++;
                    return attack / 2;
                case 3:
                    TripleAfflictionHits++;
                    return attack;
                default:
                    return 0;
            }
        }

        public void CheckLastStandHealth(int health, int maximum)
        {
            if (health > 0 && 2L * health <= maximum) LastStandActive = true;
        }

        public int TakeLastStandHit(int attack)
        {
            if (!LastStandActive || LastStandHits >= 3) return 0;
            LastStandHits++;
            return (int)Math.Max(1L, (long)attack * 60 / 100);
        }

        public void ResolveFalterMirror(float now, bool front, bool alreadyWeak, Action weaken, Func<bool> addOpening)
        {
            if (!front || now < nextMirrorTime) return;
            nextMirrorTime = now + 3f;
            weaken();
            MirrorWeakens++;
            if (alreadyWeak && addOpening()) MirrorOpenings++;
        }

        public int ThunderBellPower(bool marked, int attack)
        {
            thunderBellHits++;
            if (!marked && thunderBellHits % 3 != 0) return 0;
            return (int)Math.Max(0L, (long)attack * 60 / 100);
        }

        public int TinderLayersOnBasicHit()
        {
            tinderHits++;
            if (tinderHits == 1) return 2;
            return (tinderHits - 1) % 3 == 0 ? 1 : 0;
        }

        public T ResolveHeartLamp<T>(T primary, int actualHealing,
            IEnumerable<(T Unit, int Id, int Health, int MaxHealth)> allies, Func<T,int,int> heal) where T : class
        {
            int amount = actualHealing / 2;
            if (amount <= 0) return null;
            var target = allies.Where(a => a.Unit != primary && a.Health > 0 && a.Health < a.MaxHealth)
                .OrderBy(a => (double)a.Health / a.MaxHealth).ThenBy(a => a.Id)
                .Select(a => a.Unit).FirstOrDefault();
            if (target == null) return null;
            int restored = heal(target, amount);
            if (restored <= 0) return null;
            LampTransfers++;
            LampHealing += restored;
            return target;
        }

        public int Quakes, QuakeHits;
        public bool HasRainTassel => (EquippedEffects & ArtifactTrigger.RainTassel) != 0;
        public int RainVolleys, RainSwords, TideShields, TideBreaks, TideExpirations;
        public long RainDamage;
        public long QuakeDamage;

        public void ResolveQuake<T>(IEnumerable<T> nearbyEnemies, int attack, Func<T,int,int> hit)
        {
            Quakes++;
            foreach (var enemy in nearbyEnemies)
            {
                int dealt = hit(enemy,attack);
                if (dealt <= 0) continue;
                QuakeHits++;
                QuakeDamage += dealt;
            }
        }
        public long Damage, Healing, BonusHealing, SharedDamage, SwordDamage, OpeningSwordDamage, GrantedShield, RepeatHealing;
    }

    public sealed class ActorBattleOutput
    {
        public int ActorId { get; set; }
        public ArtifactBattleOutput Artifacts { get; set; }
        public bool Allied { get; set; }
        public bool PearlCharged;
        public int CriticalHits, MarksApplied, MarksUsed;
        public long DamageTaken { get; set; }
        public long HealthLost { get; set; }
        public IReadOnlyList<SkillBattleOutput> Skills { get; set; } = Array.Empty<SkillBattleOutput>();
    }

    public sealed class ActorState
    {
        private readonly Dictionary<int, TechniqueProgress> techniques = new Dictionary<int, TechniqueProgress>();
        public int Id { get; }
        public int BirthNodeId { get; }
        public NamelessSeal InnateSeal { get; internal set; }
        public int CombatAttackPercent { get; internal set; }
        public int CombatAttack => checked((int)((long)Attack * (100 + CombatAttackPercent) / 100));
        internal int StoryDelayYears { get; set; }
        // Null while travelling: an actor cannot be encountered at the departure node.
        public int? NodeId { get; internal set; }
        public int? TravelOriginNodeId { get; internal set; }
        public ActorMapPoint? FrozenMapPoint { get; internal set; }
        public int Age { get; internal set; }
        public int Lifespan { get; internal set; }
        public int Level { get; internal set; } = 1;
        public long LevelExperience { get; internal set; }
        public TalentProfile Talents { get; internal set; }
        public int BaseHealth { get; internal set; }
        public int BaseAttack { get; internal set; }
        public int BaseLifespan { get; internal set; }
        public int Health { get; internal set; }
        public int Attack { get; internal set; }
        public int MaxHealth { get; internal set; }
        public int? LeaderId { get; internal set; }
        public bool HasJoinedChosen { get; internal set; }
        public bool IsDisguised { get; internal set; }
        public int DisguisedYear { get; internal set; }
        public long Experience { get; internal set; }
        public long LegacySpiritStones { get; private set; }
        internal void AwardLegacySpiritStones(int amount) => LegacySpiritStones = checked(LegacySpiritStones + amount);

        internal bool TrySpendLegacySpiritStones(long amount)
        {
            if (amount < 0 || amount > LegacySpiritStones) return false;
            LegacySpiritStones -= amount;
            return true;
        }
        public (int Applications, int Transfers, int Jumps) LastLightningBattle { get; set; }
        public IReadOnlyList<ActorBattleOutput> LastBattleOutput { get; set; } = Array.Empty<ActorBattleOutput>();
        public bool HasJoinedPlayer { get; internal set; }
        public bool OnWorldMap => IsAlive && !HasJoinedPlayer;
        internal void TransferSpiritStonesTo(ActorState receiver)
        {
            receiver.LegacySpiritStones = checked(receiver.LegacySpiritStones + LegacySpiritStones);
            LegacySpiritStones = 0;
        }
        public string GuardianSpecies { get; set; }
        public bool IsAlive => Health > 0;
        public WorldAction CurrentAction { get; internal set; }
        public int RemainingYears { get; internal set; }
        public List<int> Items { get; } = new List<int>();
        public IReadOnlyDictionary<int, TechniqueProgress> Techniques => techniques;

        public void LearnTechnique(TechniqueDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (techniques.ContainsKey(definition.Id)) return;
            var progress = new TechniqueProgress(definition);
            progress.CheckItems(Items);
            techniques.Add(definition.Id, progress);
        }

        // The battle adapter reports kills credited to this technique's equipped skill.
        public void SettleTechniqueBattle(int techniqueId, int usedRank, int experience, int skillKills)
        {
            if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
            if (skillKills < 0) throw new ArgumentOutOfRangeException(nameof(skillKills));
            if (techniques.TryGetValue(techniqueId, out var progress))
                progress.SettleBattle(usedRank, experience, skillKills, Items);
        }

        internal void CheckTechniqueItems()
        {
            foreach (var progress in techniques.Values)
                progress.CheckItems(Items);
        }

        public ActorState(int id, int nodeId, int health = 100, int attack = 10, int lifespan = 100)
        {
            if (health < 1 || attack < 1 || lifespan < 1) throw new ArgumentOutOfRangeException();
            Id = id;
            BirthNodeId = nodeId;
            NodeId = nodeId;
            Health = health;
            MaxHealth = health;
            Attack = attack;
            Lifespan = lifespan;
            Talents = TalentProfile.Generate(id);
            BaseHealth = health; BaseAttack = attack; BaseLifespan = lifespan;
        }

        internal void RestoreTechnique(TechniqueProgress progress) => techniques.Add(progress.Definition.Id, progress);

        internal void CopyPersonalProgress(ActorState source)
        {
            Level = source.Level; Experience = source.Experience; LevelExperience = source.LevelExperience;
            Talents = source.Talents; Age = source.Age; Lifespan = source.Lifespan;
            BaseHealth = source.BaseHealth; BaseAttack = source.BaseAttack; BaseLifespan = source.BaseLifespan;
            CombatAttackPercent = source.CombatAttackPercent;
            Items.Clear(); Items.AddRange(source.Items);
            techniques.Clear();
            foreach (var technique in source.Techniques.Values) RestoreTechnique(technique.Copy());
        }

        internal ActorState CopyForParty(int id)
        {
            var copy = new ActorState(id, BirthNodeId, MaxHealth, Attack, Lifespan) { Health = Health, NodeId = NodeId };
            copy.CopyPersonalProgress(this);
            return copy;
        }

        internal void Die()
        {
            Health = 0;
            CurrentAction = null;
            RemainingYears = 0;
            NodeId = null;
            LeaderId = null;
            IsDisguised = false;
        }
    }
}
