using System;
using System.Collections.Generic;

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

    public sealed class ActorBattleOutput
    {
        public int ActorId { get; set; }
        public bool Allied { get; set; }
        public long DamageTaken { get; set; }
        public long HealthLost { get; set; }
        public IReadOnlyList<SkillBattleOutput> Skills { get; set; } = Array.Empty<SkillBattleOutput>();
    }

    public sealed class ActorState
    {
        private readonly Dictionary<int, TechniqueProgress> techniques = new Dictionary<int, TechniqueProgress>();
        public int Id { get; }
        public int BirthNodeId { get; }
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
