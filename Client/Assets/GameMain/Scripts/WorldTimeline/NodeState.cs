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
        public int LootAttackBonus { get; set; }
        public bool IsCultivationSite { get; set; }
        public bool IsMerchant { get; set; }
        public bool HasExplorationGuardian { get; set; }
        public bool StoryRequiresGuardianDefeat { get; set; }
        internal (int ActorId, int Year)? GuardianDefeat { get; set; }
        internal List<int> GroupGuardianHealth { get; } = new List<int>();
        public bool IsGuardianGroup { get; private set; }
        public bool GuardiansStartClose { get; set; }
        public int GuardianCount => GroupGuardianHealth.Count > 0 ? GroupGuardianHealth.Count : MonsterHealth > 0 ? 1 : 0;
        public List<int> Items { get; } = new List<int>();
        public int StoryTrialItemId { get; set; }
        public int StoryTrialStage { get; set; }
        public bool StoryTrialStarted { get; internal set; }

        public NodeState(int id) { Id = id; }

        public void SetGuardianGroup(int count, int health, int attack)
        {
            if (count < 2 || count > 28) throw new ArgumentOutOfRangeException(nameof(count));
            if (health <= 0 || attack <= 0) throw new ArgumentOutOfRangeException(nameof(health));
            SetGuardianFormation(attack, Enumerable.Repeat(health, count).ToArray());
        }

        internal void SetGuardianFormation(int attack, int[] health)
        {
            MonsterHealth = health.Sum();
            IsGuardianGroup = health.Length > 1;
            MonsterAttack = attack;
            GroupGuardianHealth.Clear();
            if (IsGuardianGroup) GroupGuardianHealth.AddRange(health);
        }
    }
}
