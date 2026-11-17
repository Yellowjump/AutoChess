using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public static class WorldPathfinder
    {
        // Stable BFS: shortest route in annual one-edge steps, ties resolved by node ID.
        public static int? NextStep(WorldState world, int start, IEnumerable<int> targets)
        {
            var route = FindRoute(world, start, targets);
            return route.Count == 0 ? (int?)null : route[0];
        }

        public static IReadOnlyList<int> FindRoute(WorldState world, int start, IEnumerable<int> targets,
            Func<NodeState, bool> canEnter = null)
        {
            var destinations = new HashSet<int>(targets);
            var queue = new Queue<int>();
            var previous = new Dictionary<int, int>();
            queue.Enqueue(start);
            previous.Add(start, start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (destinations.Contains(current))
                {
                    var route = new List<int>();
                    for (int node = current; node != start; node = previous[node]) route.Add(node);
                    route.Reverse();
                    return route;
                }
                foreach (int next in world.Nodes[current].Links.OrderBy(n => n))
                {
                    if (previous.ContainsKey(next) || !world.Nodes.TryGetValue(next, out var node) ||
                        (canEnter != null && !canEnter(node))) continue;
                    previous.Add(next, current);
                    queue.Enqueue(next);
                }
            }
            return Array.Empty<int>();
        }
    }
}
