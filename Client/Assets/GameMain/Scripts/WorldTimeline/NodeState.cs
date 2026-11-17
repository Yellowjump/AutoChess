using System.Collections.Generic;
using System;
using System.Linq;

namespace WorldTimeline
{
    public sealed class NodeState
    {
        public int Id { get; }
        public int Q { get; set; }
        public int R { get; set; }
        public HashSet<int> Links { get; } = new HashSet<int>();
        public int CultivationPerYear { get; set; } = 1;
        public int MonsterHealth { get; set; }
        public int MonsterAttack { get; set; } = 1;
        public string GuardianAppearance { get; set; }
        public bool IsCaveEncounter { get; set; }
        public int? BattleExperienceReward { get; set; }
        public int LootAttackBonus { get; set; }
        public bool GuardianOpensWithPounce { get; set; }
        public bool IsCultivationSite { get; set; }
        public bool IsMerchant { get; set; }
        public int? CompanionStageRole { get; internal set; }
        public bool IsExpeditionVanguard { get; internal set; }
        public bool HasExplorationGuardian { get; set; }
        public bool StoryRequiresGuardianDefeat { get; set; }
        public (int ActorId, int Year)? GuardianDefeat { get; internal set; }
        internal List<int> GroupGuardianHealth { get; } = new List<int>();
        public bool IsGuardianGroup { get; private set; }
        public bool GuardiansStartClose { get; set; }
        public const int CoastalRangedCount = 7;
        public const int CoastalGuardianCount = 20;
        public bool HasCoastalFormation { get; private set; }
        public int GuardianCount => GroupGuardianHealth.Count > 0
            ? GroupGuardianHealth.Count(hp => hp > 0) : MonsterHealth > 0 ? 1 : 0;

        public void SetCoastalFormation(int meleeHealth, int rangedHealth, int attack)
        {
            if (meleeHealth <= 0 || rangedHealth <= 0 || attack <= 0)
                throw new ArgumentOutOfRangeException();
            SetGuardianFormation(attack, Enumerable.Repeat(rangedHealth, CoastalRangedCount)
                .Concat(Enumerable.Repeat(meleeHealth, CoastalGuardianCount - CoastalRangedCount)).ToArray());
            HasCoastalFormation = true;
            HasExplorationGuardian = true;
        }
        public List<int> Items { get; } = new List<int>();
        public int StoryTrialItemId { get; set; }
        public int StoryTrialStage { get; set; }
        public bool StoryTrialStarted { get; internal set; }

        public NodeState(int id) { Id = id; }

        internal bool RemoveOneGuardian()
        {
            if (GuardianCount < 2) return false;
            int last = GroupGuardianHealth.FindLastIndex(hp => hp > 0);
            if (HasCoastalFormation) GroupGuardianHealth[last] = 0;
            else GroupGuardianHealth.RemoveAt(last);
            MonsterHealth = GroupGuardianHealth.Sum();
            return true;
        }

        public void SetGuardianGroup(int count, int health, int attack)
        {
            if (count < 2 || count > 28) throw new ArgumentOutOfRangeException(nameof(count));
            if (health <= 0 || attack <= 0) throw new ArgumentOutOfRangeException(nameof(health));
            SetGuardianFormation(attack, Enumerable.Repeat(health, count).ToArray());
        }

        internal void SetGuardianFormation(int attack, int[] health)
        {
            HasCoastalFormation = false;
            MonsterHealth = health.Sum();
            IsGuardianGroup = health.Length > 1;
            MonsterAttack = attack;
            GroupGuardianHealth.Clear();
            if (IsGuardianGroup) GroupGuardianHealth.AddRange(health);
        }
    }
}
