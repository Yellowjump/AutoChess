using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    // Companions belong to this expedition, not to the independent historical actors.
    // Their combat bodies are recreated each encounter; they never advance the world clock alone.
    public sealed class ExpeditionParty
    {
        public const int Capacity = 3;
        public const int FirstCompanionId = 1000001;
        private readonly WorldState world;
        public int LeaderId { get; }
        public ExpeditionEquipment Equipment { get; }
        private readonly int companionBase;
        private readonly List<RecordedExpedition> records = new List<RecordedExpedition>();
        public IReadOnlyList<RecordedExpedition> Records => records.AsReadOnly();
        public int CompanionId(int role) => companionBase + role;
        public void RecordFormation()
        {
            if (LeaderId != world.PlayerActorId) return;
            var leader = world.Actors[LeaderId];
            records.Add(new RecordedExpedition(world.Year, leader.NodeId ?? leader.BirthNodeId,
                world.FormationBattle, ProgressionEnabled, VanguardEnabled, recruited.OrderBy(r => r),
                cells.Select(id => id == 0 ? -2 : id == LeaderId ? -1 : Role(id)), Equipment.Snapshot()));
        }
        internal void Restore(RecordedExpedition record)
        {
            recruited.Clear(); recruited.UnionWith(record.Recruited);
            for (int i = 0; i < cells.Length; i++) cells[i] = record.Cells[i] == -2 ? 0 :
                record.Cells[i] == -1 ? LeaderId : CompanionId(record.Cells[i]);
            if (record.Progression && !ProgressionEnabled) EnableProgression();
            VanguardEnabled = record.CloseTaunt;
            Equipment.Restore(record.Equipment);
        }
        private readonly HashSet<int> recruited = new HashSet<int>();
        private readonly int[] cells = new int[6];
        private readonly Dictionary<int, int> sites = new Dictionary<int, int>();
        private readonly HashSet<int> explored = new HashSet<int>();
        public bool ProgressionEnabled { get; private set; }
        public bool CompanionStagesEnabled { get; private set; }
        public bool VanguardEnabled { get; private set; }
        public int? VanguardNodeId { get; private set; }

        // An explicit opt-in keeps old actions and historical battle results unchanged on replay.
        public void EnableVanguard(IEnumerable<int> storyNodes = null)
        {
            if (VanguardEnabled) return;
            VanguardEnabled = true;
            RecordFormation();
            if (!sites.TryGetValue(0, out int shieldSite)) return;
            var excluded = new HashSet<int>(storyNodes ?? Enumerable.Empty<int>());
            var player = world.Actors[LeaderId];
            // Keep the existing safe road to the first companion free of this encounter.
            if (player.NodeId.HasValue)
                excluded.UnionWith(WorldPathfinder.FindRoute(world, player.NodeId.Value, new[] { shieldSite },
                    destination => destination.MonsterHealth == 0));
            var candidate = world.Nodes.Values.Where(node => node.Id != player.BirthNodeId && node.Id != player.NodeId &&
                !excluded.Contains(node.Id) && !sites.ContainsValue(node.Id) && world.Ritual?.NodeId != node.Id &&
                !node.IsMerchant && node.MonsterHealth == 0 && !node.GuardianDefeat.HasValue &&
                !node.HasExplorationGuardian && !node.StoryRequiresGuardianDefeat && node.StoryTrialItemId == 0 && node.Items.Count == 0)
                .Select(node => new { Node = node, Route = WorldPathfinder.FindRoute(world, shieldSite, new[] { node.Id },
                    destination => destination.MonsterHealth == 0) })
                .Where(entry => entry.Route.Count > 0).OrderBy(entry => entry.Route.Count).ThenBy(entry => entry.Node.Id).FirstOrDefault();
            if (candidate == null) return;
            var guard = candidate.Node;
            VanguardNodeId = guard.Id;
            guard.IsExpeditionVanguard = true;
            guard.HasExplorationGuardian = true;
            guard.SetGuardianGroup(2, Math.Max(80, player.MaxHealth), Math.Max(12, player.MaxHealth / 6));
        }
        public bool HasExplored(int nodeId) => explored.Contains(nodeId);

        public bool RecruitmentUsesVictories(int role) => role == 1 || role == 2;
        public int RecruitmentTarget(int role) => role switch { 0 => 2, 1 => 3, 2 => 1, 3 => 5, _ => int.MaxValue };
        public int RecruitmentProgress(int role) => RecruitmentUsesVictories(role)
            ? world.Nodes.Values.Count(node => node.GuardianDefeat?.ActorId == LeaderId)
            : explored.Count(node => node != world.Actors[LeaderId].BirthNodeId);
        public bool RecruitmentConditionMet(int role) => RecruitmentProgress(role) >= RecruitmentTarget(role);

        // Old recruitment commands replay first. Only subsequent actions use stage conditions.
        public void EnableCompanionStages(IEnumerable<int> storyNodes = null)
        {
            if (CompanionStagesEnabled) return;
            CompanionStagesEnabled = true;
            var excluded = new HashSet<int>(storyNodes ?? Enumerable.Empty<int>());
            int origin = world.Actors[LeaderId].NodeId.Value;
            var distance = new Dictionary<int, int> { [origin] = 0 };
            var queue = new Queue<int>();
            queue.Enqueue(origin);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int next in world.Nodes[current].Links.OrderBy(id => id))
                    if (!distance.ContainsKey(next))
                    { distance.Add(next, distance[current] + 1); queue.Enqueue(next); }
            }
            sites.Clear();
            foreach (int role in new[] { 0, 2, 3, 1 })
            {
                var candidates = distance.Where(pair => pair.Value > 0 && !sites.ContainsValue(pair.Key) &&
                    !excluded.Contains(pair.Key) && world.Ritual?.NodeId != pair.Key &&
                    !world.Nodes[pair.Key].IsMerchant && !world.Nodes[pair.Key].StoryRequiresGuardianDefeat &&
                    world.Nodes[pair.Key].StoryTrialItemId == 0).ToArray();
                if (candidates.Length == 0) break;
                int desiredDistance = sites.Count + 2;
                int node = candidates.OrderBy(pair => role == 0 && WorldPathfinder.FindRoute(world, origin,
                        new[] { pair.Key }, destination => destination.MonsterHealth == 0).Count == 0)
                    .ThenBy(pair => world.Nodes[pair.Key].MonsterHealth > 0)
                    .ThenBy(pair => Math.Abs(pair.Value - desiredDistance)).ThenBy(pair => pair.Key).First().Key;
                sites.Add(role, node);
                world.Nodes[node].CompanionStageRole = role;
            }
        }

        // Enabled by a separate checkpoint command so old actions retain their original rewards.
        public void EnableProgression(IEnumerable<int> previousArrivals = null)
        {
            if (ProgressionEnabled) return;
            ProgressionEnabled = true;
            if (previousArrivals != null) explored.UnionWith(previousArrivals);
            if (world.Actors[LeaderId].NodeId is int current) explored.Add(current);
            RecordFormation();
        }

        public int ExplorationExperience(CultivationConfig growth)
            => ProgressExperience(growth, 5);

        public int VictoryExperience(int nodeId, CultivationConfig growth)
            => Math.Max(world.Nodes[nodeId].BattleExperienceReward ?? checked(3 * Math.Max(1, world.Nodes[nodeId].CultivationPerYear)),
                ProgressExperience(growth, 2));

        private int ProgressExperience(CultivationConfig growth, int divisor)
        {
            var player = world.Actors[LeaderId];
            long required = growth?.GetLevel(player.Level).ExperienceToNext ?? 6;
            return (int)Math.Min(int.MaxValue, Math.Max(1, required / divisor + (required % divisor == 0 ? 0 : 1)));
        }

        internal void Explore(int nodeId, CultivationConfig growth, Action<WorldEvent> emit)
        {
            var player = world.Actors[LeaderId];
            if (!ProgressionEnabled || !player.IsAlive || (LeaderId == world.PlayerActorId && world.PlayerControlEnded) ||
                world.Nodes[nodeId].MonsterHealth > 0 || !explored.Add(nodeId)) return;
            int experience = ExplorationExperience(growth);
            CultivationSystem.Gain(player, experience, growth, world.Year, emit, world, "exploration");
            emit(new WorldEvent(world.Year, WorldEventKind.Explored, player.Id, nodeId, experience.ToString()));
        }

        internal int RecoverAfterVictory()
        {
            var player = world.Actors[LeaderId];
            if (!ProgressionEnabled || !player.IsAlive) return 0;
            int recovery = Math.Min(player.MaxHealth - player.Health, Math.Max(1, player.MaxHealth / 4));
            player.Health += recovery;
            return recovery;
        }
        public IReadOnlyList<int> Cells => Array.AsReadOnly(cells);
        public IReadOnlyDictionary<int, int> RecruitmentSites => sites;
        public int DeployedCount => cells.Count(id => id != 0);
        public static bool IsCompanion(int id) => id >= FirstCompanionId && id < FirstCompanionId + 64;
        public static int Role(int id) => IsCompanion(id) ? (id - FirstCompanionId) % 4 : -1;
        public bool HasRecruited(int role) => recruited.Contains(role);
        public int CellOf(int id) => Array.IndexOf(cells, id);
        public bool Owns(int id) => id == LeaderId || id >= companionBase && id < companionBase + 4 && recruited.Contains(Role(id));

        public ExpeditionParty(WorldState world, int? leaderId = null, int companionOffset = 0)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            LeaderId = leaderId ?? world.PlayerActorId;
            Equipment = new ExpeditionEquipment(this,world);
            companionBase = FirstCompanionId + companionOffset;
            var player = world.Actors[LeaderId];
            if (!player.NodeId.HasValue) throw new InvalidOperationException("Recruitment needs a map location.");
            cells[4] = player.Id;
            int origin = player.NodeId.Value;
            var distance = new Dictionary<int, int> { [origin] = 0 };
            var queue = new Queue<int>();
            queue.Enqueue(origin);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int next in world.Nodes[current].Links.OrderBy(id => id))
                    if (!distance.ContainsKey(next))
                    { distance.Add(next, distance[current] + 1); queue.Enqueue(next); }
            }
            sites.Add(0, origin);
            RecordFormation();
            // Put the next three recruits along reachable, nearby roads. Prefer safe stops.
            foreach (int role in new[] { 2, 3, 1 })
            {
                int desiredDistance = sites.Count;
                var candidates = distance.Where(pair => pair.Value >= desiredDistance &&
                    !sites.ContainsValue(pair.Key) && world.Ritual?.NodeId != pair.Key).ToArray();
                if (candidates.Length == 0) candidates = distance.Where(pair => !sites.ContainsValue(pair.Key)).ToArray();
                if (candidates.Length == 0) break;
                int node = candidates.OrderBy(pair => pair.Value)
                    .ThenBy(pair => world.Nodes[pair.Key].MonsterHealth > 0)
                    .ThenBy(pair => pair.Key).First().Key;
                sites.Add(role, node);
            }
        }

        public bool CanRecruit(int role)
        {
            var player = world.Actors[LeaderId];
            return !world.IsEnded && !(LeaderId == world.PlayerActorId && world.PlayerControlEnded) && player.IsAlive && player.CurrentAction == null &&
                !recruited.Contains(role) && sites.TryGetValue(role, out int node) && player.NodeId == node &&
                world.Nodes[node].MonsterHealth == 0 && (!CompanionStagesEnabled || RecruitmentConditionMet(role));
        }

        public bool Recruit(int role)
        {
            if (!CanRecruit(role)) return false;
            recruited.Add(role);
            if (DeployedCount < Capacity)
            {
                int[] preference = role <= 1 ? new[] { 1, 0, 2, 3, 5, 4 } : new[] { 3, 5, 4, 0, 2, 1 };
                cells[preference.First(cell => cells[cell] == 0)] = CompanionId(role);
            }
            RecordFormation();
            return true;
        }

        public bool Place(int id, int cell)
        {
            if (!Owns(id) || cell < 0 || cell >= cells.Length) return false;
            int old = CellOf(id);
            if (old == cell) return true;
            // The player must stay deployed. Bench companions replace companions only.
            if (old < 0 && cells[cell] == LeaderId) return false;
            if (old < 0 && cells[cell] == 0 && DeployedCount >= Capacity) return false;
            if (old >= 0) cells[old] = cells[cell];
            cells[cell] = id;
            RecordFormation();
            return true;
        }

        public bool Bench(int id)
        {
            int cell = CellOf(id);
            if (id == LeaderId || !Owns(id) || cell < 0) return false;
            cells[cell] = 0;
            RecordFormation();
            return true;
        }

        public ActorState CreateCompanion(int role, int nodeId)
        {
            if (role < 0 || role > 3) throw new ArgumentOutOfRangeException(nameof(role));
            var player = world.Actors[LeaderId];
            int hpPercent = new[] { 180, 95, 70, 80 }[role];
            int attackPercent = new[] { 30, 80, 75, 20 }[role];
            return new ActorState(CompanionId(role), nodeId,
                Math.Max(1, checked((int)((long)Equipment.UnmodifiedLeaderHealth * hpPercent / 100)))+Equipment.HealthBonus(CompanionId(role)),
                Math.Max(1, checked((int)((long)Equipment.UnmodifiedLeaderAttack * (100+player.CombatAttackPercent) / 100 * attackPercent / 100)))+Equipment.AttackBonus(CompanionId(role)));
        }

        public List<ActorState> CreateCombatants(int nodeId)
        {
            var result = new List<ActorState> { world.Actors[LeaderId] };
            result.AddRange(cells.Where(IsCompanion).Select(id => CreateCompanion(Role(id), nodeId)));
            return result;
        }
    }
}
