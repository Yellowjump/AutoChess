using System;
using System.Collections.Generic;

namespace WorldTimeline
{
    public static class HexWorldMap
    {
        public const int Radius = 10;
        public static int Id(int q, int r) => checked((q + 128) * 257 + r + 128);
        public static int Distance(int q, int r) => Math.Max(Math.Abs(q), Math.Max(Math.Abs(r), Math.Abs(q + r)));

        public static Dictionary<int, NodeState> Create(int radius = Radius)
        {
            if (radius < 1 || radius > 100) throw new ArgumentOutOfRangeException(nameof(radius));
            var nodes = new Dictionary<int, NodeState>();
            for (int q = -radius; q <= radius; q++)
                for (int r = -radius; r <= radius; r++)
                {
                    if (Distance(q, r) > radius) continue;
                    int distance = Distance(q, r);
                    var node = new NodeState(Id(q, r)) { Q = q, R = r, CultivationPerYear = radius + 1 - distance };
                    int hash = Math.Abs(q * 71 + r * 131);
                    if (hash % 9 == 2)
                    {
                        node.MonsterHealth = 40 + (radius - distance) * 25;
                        node.MonsterAttack = 4 + (radius - distance) * 4;
                    }
                    else if (hash % 13 == 5) node.Items.Add(node.Id);
                    nodes.Add(node.Id, node);
                }
            int[,] directions = { { 1, 0 }, { 1, -1 }, { 0, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };
            foreach (var node in nodes.Values)
                for (int i = 0; i < 6; i++)
                {
                    int target = Id(node.Q + directions[i, 0], node.R + directions[i, 1]);
                    if (nodes.ContainsKey(target)) node.Links.Add(target);
                }
            return nodes;
        }
    }
}
