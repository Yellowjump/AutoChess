using System.Collections.Generic;

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
        public List<int> Items { get; } = new List<int>();

        public NodeState(int id) { Id = id; }
    }
}
