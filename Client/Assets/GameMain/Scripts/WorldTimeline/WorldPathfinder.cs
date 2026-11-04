using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public static class WorldPathfinder
    {
        // Stable BFS: shortest route in annual one-edge steps, ties resolved by node ID.
        public static int? NextStep(WorldState world, int start, IEnumerable<int> targets)
        {
            var destinations = new HashSet<int>(targets);
            var queue = new Queue<int>();
            var firstSteps = new Dictionary<int, int>();
            queue.Enqueue(start);
            firstSteps.Add(start, start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (destinations.Contains(current)) return current == start ? (int?)null : firstSteps[current];
                foreach (int next in world.Nodes[current].Links.OrderBy(n => n))
                {
                    if (!world.Nodes.ContainsKey(next) || firstSteps.ContainsKey(next)) continue;
                    firstSteps[next] = current == start ? next : firstSteps[current];
                    queue.Enqueue(next);
                }
            }
            return null;
        }
    }
}
