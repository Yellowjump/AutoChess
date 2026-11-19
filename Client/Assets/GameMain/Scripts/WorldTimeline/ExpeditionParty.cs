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
        public const int StageRoles = 4;
        public const int MaxPastLeaders = 6;
        public int RoleCount => StageRoles + pastLeaders.Count;
        private readonly List<ActorState> pastLeaders = new List<ActorState>();
        public const int SandSite = 100, TideSite = 101;
        private static int SiteForBirth(int birth) => birth == HexWorldMap.Id(10,-8) ? SandSite :
            birth == HexWorldMap.Id(8,2) ? TideSite : -1;
        public int NamedSite(int role) => role >= StageRoles && role < RoleCount ? SiteForBirth(PastBirth(role)) : -1;
        public int SiteMember(int site) => site < SandSite ? site :
            Enumerable.Range(StageRoles,pastLeaders.Count).Where(role => NamedSite(role) == site).DefaultIfEmpty(-1).First();
        public void EnableSignatureArtifacts()
        {
            world.SignatureArtifacts = true;
            foreach (var pair in new[]{(SandSite,HexWorldMap.Id(10,-8)),(TideSite,HexWorldMap.Id(8,2))})
            {
                if (sites.ContainsKey(pair.Item1) || !world.Nodes.TryGetValue(pair.Item2,out var home)) continue;
                var location = world.Nodes.Values.Where(n => !n.CompanionStageRole.HasValue && n.Id != world.Ritual?.NodeId &&
                    !n.IsMerchant && !n.IsExpeditionVanguard).OrderBy(n => HexWorldMap.Distance(n.Q-home.Q,n.R-home.R)).ThenBy(n=>n.Id).FirstOrDefault();
                if (location == null) continue;
                sites.Add(pair.Item1,location.Id);
                location.CompanionStageRole = pair.Item1;
            }
            foreach (var other in world.HistoricalExpeditions.Values)
                foreach (var site in sites.Where(s => s.Key >= SandSite)) other.sites[site.Key] = site.Value;
        }
        public int PastBirth(int role) => pastLeaders[role - StageRoles].BirthNodeId;
        public IEnumerable<int> LivingRoles => recruited.Where(IsSurvivingCompanion).OrderBy(role => role);
        public int CompanionId(int role) => role < StageRoles ? companionBase + role :
            companionBase + (role < 8 ? 64 + role - StageRoles : 128 + role - 8);

        // Roster admission only. Encounter choice and dissolving the source party belong to the encounter system.
        internal bool CanAddPastLeader(int sourceId)
        {
            if (sourceId == LeaderId || !AttritionEnabled || !Equipment.Modern || !world.Actors.TryGetValue(sourceId, out var source) ||
                !source.IsAlive || source.HasJoinedPlayer || source.HasJoinedChosen ||
                !world.PlayableBirths.Contains(source.BirthNodeId) || source.BirthNodeId == world.Actors[LeaderId].BirthNodeId ||
                pastLeaders.Any(actor => actor.BirthNodeId == source.BirthNodeId) || pastLeaders.Count >= MaxPastLeaders || pastLeaders.Count(actor => SiteForBirth(actor.BirthNodeId) < 0) >= 4)
                return false;
            var sourceParty = world.ExpeditionFor(sourceId);
            return sourceParty == null || sourceParty.LeaderRole == -1 && Equipment.CanImportLoadout(sourceParty.Equipment);
        }

        internal bool AddPastLeader(int sourceId,bool record = true)
        {
            if (!CanAddPastLeader(sourceId)) return false;
            var source = world.Actors[sourceId];
            var sourceParty = world.ExpeditionFor(sourceId);
            int role = RoleCount;
            var member = source.CopyForParty(CompanionId(role));
            member.MaxHealth = sourceParty?.Equipment.UnmodifiedLeaderHealth ?? source.MaxHealth;
            member.Attack = sourceParty?.Equipment.UnmodifiedLeaderAttack ?? source.Attack;
            pastLeaders.Add(member);
            remainingHealth[role] = source.Health;
            Equipment.ImportLeaderLoadout(sourceParty?.Equipment, role);
            AddToRoster(role,record);
            return true;
        }

        // The world actor is the persistent expedition token (travel, story and action log).
        // Its combat role and equipment owner change when another member takes command.
        private readonly int[] remainingHealth = Enumerable.Repeat(-1, StageRoles + MaxPastLeaders).ToArray();
        private int fallenMask, healthBasisOffset, attackBasisOffset;
        public bool AttritionEnabled { get; private set; }
        public int LeaderRole { get; private set; } = -1;
        internal int HistoricalSuccessor { get; private set; } = -1;
        public int CombatRole(int id) => id == LeaderId ? LeaderRole : Role(id);
        public int FieldId(int role) => LeaderRole == role ? LeaderId : CompanionId(role);
        public bool IsFallen(int role) => role >= SandSite ? SiteMember(role) >= 0 && IsFallen(SiteMember(role)) : (fallenMask & (1 << (role + 1))) != 0;
        public bool IsSurvivingCompanion(int role) => recruited.Contains(role) && !IsFallen(role) && role != LeaderRole;
        public int CompanionHealth(int role) => remainingHealth[role] < 0 ? CompanionMaxHealth(role)
            : Math.Min(remainingHealth[role], CompanionMaxHealth(role));
        public void EnableAttrition() { if (!AttritionEnabled) { AttritionEnabled = true; RecordFormation(); } }

        internal void RetainCombatProgress(ActorState combatant)
        {
            int role = Role(combatant.Id);
            if (role >= StageRoles) pastLeaders[role - StageRoles].CopyPersonalProgress(combatant);
        }

        internal void SetCompanionHealth(int role, int hp)
        {
            remainingHealth[role] = hp;
            if (hp == 0) RemoveFallen(role, CompanionId(role));
        }

        internal void RemoveFallen(int role, int actor)
        {
            if (IsFallen(role)) return;
            Equipment.ReturnLoadout(actor);
            fallenMask |= 1 << (role + 1);
            int cell = CellOf(actor);
            if (cell >= 0 && actor != LeaderId) cells[cell] = 0;
        }

        internal void Promote(int role)
        {
            if (!IsSurvivingCompanion(role)) throw new InvalidOperationException("接任者必须是存活的队员。");
            var leader = world.Actors[LeaderId];
            var next = CreateCompanion(role, leader.NodeId ?? leader.BirthNodeId);
            int basisHp = Equipment.UnmodifiedLeaderHealth + healthBasisOffset;
            int basisAttack = Equipment.UnmodifiedLeaderAttack + attackBasisOffset;
            int oldCell = CellOf(LeaderId), nextCell = CellOf(next.Id);
            if (nextCell >= 0) { cells[oldCell] = 0; cells[nextCell] = LeaderId; }
            if (role >= StageRoles) leader.CopyPersonalProgress(next);
            LeaderRole = role;
            Equipment.AdoptLeaderBonuses();
            leader.MaxHealth = next.MaxHealth;
            leader.Attack = next.Attack;
            leader.CombatAttackPercent = next.CombatAttackPercent;
            leader.Health = next.Health;
            healthBasisOffset = basisHp - Equipment.UnmodifiedLeaderHealth;
            attackBasisOffset = basisAttack - Equipment.UnmodifiedLeaderAttack;
            if (LeaderId == world.PlayerActorId && records.Count > 0)
                records[records.Count - 1].Successor = role;
        }

        public void RecordFormation()
        {
            if (LeaderId != world.PlayerActorId) return;
            var leader = world.Actors[LeaderId];
            if (LeaderRole >= StageRoles)
            {
                var member = pastLeaders[LeaderRole - StageRoles];
                member.CopyPersonalProgress(leader);
                member.MaxHealth = Equipment.UnmodifiedLeaderHealth;
                member.Attack = Equipment.UnmodifiedLeaderAttack;
                remainingHealth[LeaderRole] = leader.Health;
            }
            records.Add(new RecordedExpedition(world.Year, leader.NodeId ?? leader.BirthNodeId,
                world.FormationBattle, ProgressionEnabled, VanguardEnabled, recruited.OrderBy(r => r),
                cells.Select(id => id == 0 ? -2 : id == LeaderId ? -1 : Role(id)), Equipment.Snapshot(), AttritionEnabled ? new[] { LeaderRole, fallenMask, healthBasisOffset, attackBasisOffset }.Concat(remainingHealth.Take(RoleCount)) : null, pastLeaders: pastLeaders));
        }
        internal void EnableHistoricalGrowth()
        {
            EnableProgression();
            EnableAttrition();
            Equipment.EnableArtifacts();
            Equipment.EnableLinks();
            if (world.Expedition?.CompanionStagesEnabled == true)
            {
                CompanionStagesEnabled = true;
                sites.Clear();
                foreach (var entry in world.Expedition.RecruitmentSites) sites.Add(entry.Key,entry.Value);
            }
        }

        internal void ApplyHistoricalPlan(RecordedExpedition previous, RecordedExpedition plan, IReadOnlyDictionary<int,int> rewardOrigins)
        {
            VanguardEnabled = plan.CloseTaunt;
            int MapRole(int role)
            {
                if (role < StageRoles) return role;
                int index = pastLeaders.FindIndex(actor => actor.BirthNodeId == plan.PastLeaders[role-StageRoles].BirthNodeId);
                return index < 0 ? -2 : StageRoles+index;
            }
            foreach (int role in plan.Recruited)
                if (previous?.Recruited.Contains(role) != true)
                {
                    if (role < StageRoles) Recruit(role);
                    else if (SiteForBirth(plan.PastLeaders[role-StageRoles].BirthNodeId) is int site && site >= 0) Recruit(site);
                }

            HistoricalSuccessor = MapRole(plan.Successor);
            // An archived succession or casualty is an outcome, not a formation order.
            bool archivedLoss = plan.Vitality.Count > 0 && previous?.Vitality.Count > 0 &&
                previous.Vitality[1] != plan.Vitality[1];
            if (archivedLoss) return;
            if (previous == null || !plan.Cells.SequenceEqual(previous.Cells))
            {
                for (int cell = 0; cell < cells.Length; cell++)
                {
                    int id = cells[cell];
                    if (id != 0 && id != LeaderId && !plan.Cells.Select(MapRole).Contains(Role(id))) Bench(id);
                }
                for (int cell = 0; cell < cells.Length; cell++)
                {
                    int role = MapRole(plan.Cells[cell]);
                    if (role == -1) Place(LeaderId,cell);
                    else if (IsSurvivingCompanion(role)) Place(CompanionId(role),cell);
                }
            }
            Equipment.ApplyHistoricalPlan(previous?.Equipment,plan.Equipment,world.Nodes[plan.NodeId].IsMerchant,rewardOrigins,MapRole);
        }

        internal void Restore(RecordedExpedition record)
        {
            pastLeaders.Clear();
            foreach (var member in record.PastLeaders) pastLeaders.Add(member.CopyForParty(CompanionId(RoleCount)));
            recruited.Clear(); recruited.UnionWith(record.Recruited.Where(role => role >= StageRoles ? NamedSite(role) < 0 || world.ClaimStage(NamedSite(role),LeaderId) : world.ClaimStage(role, LeaderId)));
            for (int i = 0; i < cells.Length; i++) cells[i] = record.Cells[i] == -2 ? 0 :
                record.Cells[i] == -1 ? LeaderId : recruited.Contains(record.Cells[i]) ? CompanionId(record.Cells[i]) : 0;
            if (record.Progression && !ProgressionEnabled) EnableProgression();
            VanguardEnabled = record.CloseTaunt;
            AttritionEnabled = record.Vitality.Count > 0;
            if (AttritionEnabled)
            {
                LeaderRole = recruited.Contains(record.Vitality[0]) ? record.Vitality[0] : -1; fallenMask = record.Vitality[1];
                healthBasisOffset = record.Vitality[2]; attackBasisOffset = record.Vitality[3];
                for (int i = 0; i < RoleCount; i++) remainingHealth[i] = record.Vitality[4+i];
            }
            HistoricalSuccessor = record.Successor;
            Equipment.Restore(record.Equipment);
            if (LeaderRole >= StageRoles)
            {
                var leader = world.Actors[LeaderId];
                var member = CreateCompanion(LeaderRole, leader.NodeId ?? leader.BirthNodeId);
                leader.CopyPersonalProgress(member);
                leader.MaxHealth = member.MaxHealth; leader.Attack = member.Attack; leader.Health = member.Health;
            }
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
        public int RecruitmentTarget(int role) => role switch { 0 => 2, 1 => 3, 2 => 1, 3 => 5, SandSite or TideSite => 0, _ => int.MaxValue };
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
            if (world.SharedClears && world.Nodes[nodeId].FirstClear?.ActorId != LeaderId) return;
            int experience = ExplorationExperience(growth);
            CultivationSystem.Gain(player, experience, growth, world.Year, emit, world, "exploration");
            GrowPastLeaders(experience, growth, emit);
            emit(new WorldEvent(world.Year, WorldEventKind.Explored, player.Id, nodeId, experience.ToString()));
        }

        internal void GrowPastLeaders(int experience, CultivationConfig growth, Action<WorldEvent> emit)
        {
            foreach (int role in LivingRoles.Where(role => role >= StageRoles))
            {
                var member = pastLeaders[role - StageRoles];
                member.NodeId = world.Actors[LeaderId].NodeId;
                CultivationSystem.Gain(member, experience, growth, world.Year, emit);
            }
        }

        public const int RestPercent = 25;
        public bool NeedsRest => world.Actors[LeaderId].Health < world.Actors[LeaderId].MaxHealth ||
            recruited.Any(role => IsSurvivingCompanion(role) && CompanionHealth(role) < CompanionMaxHealth(role));

        internal void Rest()
        {
            var captain = world.Actors[LeaderId];
            captain.Health = RestedHealth(captain.Health, captain.MaxHealth);
            foreach (int role in recruited.Where(IsSurvivingCompanion))
                remainingHealth[role] = RestedHealth(CompanionHealth(role), CompanionMaxHealth(role));
        }

        private static int RestedHealth(int health, int maxHealth) =>
            (int)Math.Min(maxHealth, health + Math.Max(1L, (long)maxHealth * RestPercent / 100));

        public int KnotBearerFor(int frontCell)
        {
            if (frontCell < 0 || frontCell >= 3) return 0;
            int rear = cells[frontCell + 3];
            return rear != 0 && (Equipment.BattleTriggers(rear) & ArtifactTrigger.ComradeKnot) != 0 ? rear : 0;
        }

        internal (int Role, int Amount) TreatWithFieldKit()
        {
            var captain = world.Actors[LeaderId];
            var target = Enumerable.Repeat(LeaderRole, 1).Concat(LivingRoles)
                .Select(role => (Role: role, Health: role == LeaderRole ? captain.Health : CompanionHealth(role),
                    Maximum: role == LeaderRole ? captain.MaxHealth : CompanionMaxHealth(role)))
                .Where(member => member.Health > 0 && member.Health < member.Maximum)
                .OrderBy(member => (decimal)member.Health / member.Maximum).FirstOrDefault();
            if (target.Maximum == 0) return default;
            int amount = Math.Min(target.Maximum - target.Health, Math.Max(1, (int)(12L * target.Maximum / 100)));
            if (target.Role == LeaderRole) captain.Health += amount;
            else SetCompanionHealth(target.Role, target.Health + amount);
            RecordFormation();
            return (target.Role, amount);
        }

        internal int RecoverAfterVictory()
        {
            var player = world.Actors[LeaderId];
            if (AttritionEnabled || !ProgressionEnabled || !player.IsAlive) return 0;
            int recovery = Math.Min(player.MaxHealth - player.Health, Math.Max(1, player.MaxHealth / 4));
            player.Health += recovery;
            return recovery;
        }
        public IReadOnlyList<int> Cells => Array.AsReadOnly(cells);
        public IReadOnlyDictionary<int, int> RecruitmentSites => sites;
        public int DeployedCount => cells.Count(id => id != 0 && Owns(id));
        public static bool IsCompanion(int id) => id >= FirstCompanionId && id < FirstCompanionId + 192;
        public static int Role(int id) => IsCompanion(id) ? (id - FirstCompanionId) % 4 + (id >= FirstCompanionId + 128 ? 8 : id >= FirstCompanionId + 64 ? StageRoles : 0) : -1;
        public bool HasRecruited(int role) => recruited.Contains(SiteMember(role));
        public int CellOf(int id) => Array.IndexOf(cells, id);
        public bool Owns(int id) => id == LeaderId ? !IsFallen(LeaderRole) : IsCompanion(id) && id == CompanionId(Role(id)) && IsSurvivingCompanion(Role(id));

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
                (!world.SharedClears || !world.StageOwner(role).HasValue || world.StageOwner(role) == LeaderId) &&
                !HasRecruited(role) && (role < SandSite || pastLeaders.Count < MaxPastLeaders) && sites.TryGetValue(role, out int node) && player.NodeId == node &&
                world.Nodes[node].MonsterHealth == 0 && (!CompanionStagesEnabled || RecruitmentConditionMet(role));
        }

        public bool Recruit(int role)
        {
            if (!CanRecruit(role) || !world.ClaimStage(role,LeaderId)) return false;
            int site = role;
            if (site == SandSite || site == TideSite)
            {
                role = RoleCount;
                int birth = site == SandSite ? HexWorldMap.Id(10,-8) : HexWorldMap.Id(8,2);
                var member = new ActorState(CompanionId(role),birth,
                    Math.Max(1,Equipment.UnmodifiedLeaderHealth * (site == SandSite ? 150 : 90) / 100),
                    Math.Max(1,Equipment.UnmodifiedLeaderAttack * (site == SandSite ? 45 : 70) / 100));
                member.Items.Add(site == SandSite ? 8105 : 8107);
                pastLeaders.Add(member);
                Equipment.GrantNamedInnate(role);
            }
            if (world.ReturnedStageHealth(site) is int hp)
            {
                Equipment.ClearReturnedInnate(role);
                remainingHealth[role] = Math.Min(hp,CompanionMaxHealth(role));
                world.TakeReturnedStage(site);
            }
            AddToRoster(role);
            return true;
        }

        private void AddToRoster(int role,bool record = true)
        {
            recruited.Add(role);
            if (DeployedCount < Capacity)
            {
                int[] preference = role <= 1 || NamedSite(role) == SandSite ? new[] { 1, 0, 2, 3, 5, 4 } : new[] { 3, 5, 4, 0, 2, 1 };
                cells[preference.First(cell => cells[cell] == 0)] = CompanionId(role);
            }
            if (record) RecordFormation();
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

        public int CompanionMaxHealth(int role)
        {
            if (role >= StageRoles) return checked(pastLeaders[role - StageRoles].MaxHealth + Equipment.HealthBonus(CompanionId(role)));
            int hpPercent = role switch { 0 => 180, 1 => 95, 2 => 70, 3 => 80, _ => throw new ArgumentOutOfRangeException(nameof(role)) };
            return Math.Max(1, checked((int)((long)(Equipment.UnmodifiedLeaderHealth + healthBasisOffset) * hpPercent / 100))) + Equipment.HealthBonus(CompanionId(role));
        }

        public ActorState CreateCompanion(int role, int nodeId)
        {
            if (role >= StageRoles && role < RoleCount)
            {
                var member = pastLeaders[role - StageRoles].CopyForParty(CompanionId(role));
                member.NodeId = nodeId; member.Health = CompanionHealth(role); member.MaxHealth = CompanionMaxHealth(role);
                member.Attack = checked(member.Attack + Equipment.AttackBonus(member.Id));
                return member;
            }
            if (role < 0 || role > 3) throw new ArgumentOutOfRangeException(nameof(role));
            var player = world.Actors[LeaderId];
            int attackPercent = new[] { 30, 80, 75, 20 }[role];
            return new ActorState(CompanionId(role), nodeId,
                CompanionMaxHealth(role),
                Math.Max(1, checked((int)((long)(Equipment.UnmodifiedLeaderAttack + attackBasisOffset) * (100+player.CombatAttackPercent) / 100 * attackPercent / 100)))+Equipment.AttackBonus(CompanionId(role))) { Health = CompanionHealth(role) };
        }

        public List<ActorState> CreateCombatants(int nodeId)
        {
            var result = new List<ActorState> { world.Actors[LeaderId] };
            result.AddRange(cells.Where(IsCompanion).Select(id => CreateCompanion(Role(id), nodeId)));
            return result;
        }
    }
}
