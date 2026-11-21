using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        public static string BalanceLogDirectory => Path.Combine(SaveDirectory, "BalanceLogs");

        private string balanceConfigVersion;

        private string GetBalanceConfigVersion()
        {
            var world = simulation.State;
            var input = new StringBuilder();
            if (simulation.Cultivation != null)
                foreach (var level in simulation.Cultivation.Levels)
                {
                    input.Append(level.ExperienceToNext).Append(';');
                    foreach (GrowthAttribute attribute in Enum.GetValues(typeof(GrowthAttribute)))
                        foreach (TalentGrade grade in Enum.GetValues(typeof(TalentGrade)))
                        {
                            var range = level.Range(attribute, grade);
                            input.Append(range.Min).Append(',').Append(range.Max).Append(';');
                        }
                }
            foreach (var node in world.Nodes.Values.OrderBy(node => node.Id))
                input.AppendLine($"{node.Id},{node.CultivationPerYear},{node.MonsterHealth},{node.MonsterAttack}," +
                    $"{node.GuardianCount},{node.GuardianAppearance},{node.GuardianOpensWithPounce},{node.BattleExperienceReward},{node.LootAttackBonus},{node.StoryTrialStage},{string.Join(",", node.Items)}");
            if (activeChosenRoute != null)
                foreach (var stage in activeChosenRoute)
                    input.AppendLine($"{stage.NodeId},{stage.TravelYears},{stage.RitualPeriod}");
            if (sluiceCampaignActive)
                input.AppendLine($"Initial deadline: {world.Ritual.InitialDeadlineYear}, sluice: {OpenSluiceOption}");
            if (world.SluiceDrainsBoss)
                input.AppendLine($"Sluice boss health reduction: {FinalRitualSystem.OpenSluiceHealthReductionPercent}%");
            if (northRelayActive)
                input.AppendLine($"North relay: {RestoreNorthRelayOption}, boss attack reduction: {FinalRitualSystem.NorthRelayAttackReductionPercent}%");
            if (world.NorthRelaySpringNodeId is int spring)
                input.AppendLine($"North relay spring: {spring}");
            if (contractStoryActive)
                input.AppendLine($"Contract revocation: {RevokeContractOption}, gate: {TwinGolemGate}");
            if (world.CaveRegionsEnabled)
                input.AppendLine("Ice and lava cave encounters");
            if (simulation.Stories != null)
                foreach (var option in simulation.Stories.Config.Options.Values.OrderBy(option => option.Id))
                    input.AppendLine($"{option.EventId},{option.Id},{option.Experience},{option.ItemId},{option.CostItemId}," +
                        $"{option.HealthCost},{option.Heal},{option.TimeYears},{option.LegacyPrice}");
            input.Append($"{world.BattleExperienceEnabled},{world.StoryCombatSkillsEnabled}," +
                $"{world.TechniqueCombatEnabled},{world.BalancedLightningTrials}");
            if (world.AncientSwordWaitsForHit) input.AppendLine("Ancient sword opens after a basic hit");
            if (world.ShortAncientSwordOpening) input.AppendLine("Three blades in the first ancient sword volley");
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToString()));
            return Application.version + "/" + BitConverter.ToString(hash, 0, 8).Replace("-", "");
        }

        private void StartBalanceLog(string runId, bool resumed)
        {
            var world = simulation.State;
            var segmentId = Guid.NewGuid().ToString("N");
            var segmentStartedUtc = DateTime.UtcNow.ToString("O");
            var directory = BalanceLogDirectory;
            var path = Path.Combine(directory, $"run_{runId}_{segmentId}.jsonl");
            var encoding = new UTF8Encoding(false);
            var configVersion = balanceConfigVersion;
            int sequence = 0;

            world.BalanceSink = sample =>
            {
                try
                {
                    sample.runId = runId;
                    sample.segmentId = segmentId;
                    sample.segmentStartedUtc = segmentStartedUtc;
                    sample.configVersion = configVersion;
                    sample.seq = ++sequence;
                    if (locations.TryGetValue(sample.nodeId, out var location))
                    {
                        sample.nodeName = location.Name;
                        sample.locationConfigId = location.ConfigId;
                    }
                    Directory.CreateDirectory(directory);
                    File.AppendAllText(path, JsonUtility.ToJson(sample) + "\n", encoding);
                }
                catch (IOException error)
                {
                    world.BalanceSink = null;
                    Debug.LogWarning("养成日志已停止写入：" + error.Message);
                }
                catch (UnauthorizedAccessException error)
                {
                    world.BalanceSink = null;
                    Debug.LogWarning("养成日志已停止写入：" + error.Message);
                }
            };

            var player = world.Actors[world.PlayerActorId];
            world.RecordBalance(player, new BalanceSample
            {
                type = resumed ? "run_resumed" : "run_started",
                source = $"round {round}",
                items = player.Items.ToArray(),
                thresholds = simulation.Cultivation?.Levels.Select(level => level.ExperienceToNext).ToArray()
            });
            Debug.Log("养成日志目录：" + directory);
        }
    }
}
