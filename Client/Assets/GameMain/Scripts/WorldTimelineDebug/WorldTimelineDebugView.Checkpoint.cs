using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineDebugView
    {
        [Serializable]
        private sealed class Checkpoint
        {
            public const int LatestVersion = 57;
            public int version = LatestVersion, round, item, selectedNode, year, health, attack, level;
            public bool openingMap, openingPrompt, guidedDiscovery;
            public int openingChoice;
            public bool ended;
            public bool hasLifespanCutoff;
            public int lifespanThroughYear;
            public int battleRewardsFromYear;
            public int regionalGuardiansFromYear;
            public int outerGuardiansFromYear;
            public long spiritStones;
            public bool hasLegacyTradeCutoff;
            public int legacyTradeThroughYear;
            public string histories, balanceRunId;
            public ChosenRouteStage[] chosenRoute;
            public List<CheckpointAction> actions = new List<CheckpointAction>();
            public List<CheckpointStoryChoice> storyChoices = new List<CheckpointStoryChoice>();
        }
        [Serializable] private sealed class CheckpointStoryChoice
        {
            public int eventId, occurrence, optionId;
            public CheckpointStoryChoice(RecordedStoryChoice choice)
            { eventId = choice.EventId; occurrence = choice.Occurrence; optionId = choice.OptionId; }
            public RecordedStoryChoice ToChoice() => new RecordedStoryChoice(eventId, occurrence, optionId);
        }

        [Serializable]
        private sealed class CheckpointAction
        {
            public const int GrantNativeItem = -2;
            public const int EnableExpedition = -3, RecruitCompanion = -4, PlaceCompanion = -5, BenchCompanion = -6;
            public const int EnableMapProgression = -7;
            public const int EnableCompanionStages = -8;
            public const int EnableVanguard = -9;
            public const int EnableTeamEquipment=-10, EquipTeamItem=-11, UnequipTeamItem=-12,
                UseTeamItem=-13, ClaimTeamReward=-14;
            public const int EnableArtifacts=-15, EquipArtifact=-16, BuyArtifact=-17, UpgradeArtifact=-18, EventUpgrade=-19;
            public const int EquipArtifactSixSlots=-20;
            public const int TransferArtifact=-21, EnableAttrition=-22, SelectSuccessor=-23, AddPastLeader=-24, EnableArtifactLinks=-25, EnableSharedClears=-26, EnableHistoricalGrowth=-27, EnablePastInvitations=-28, PastInvitationChoice=-29, EnableSignatureArtifacts=-30, EnableSwordTraining=-31, EnableQuakeSpike=-32, EnableRainTassel=-33, EnableRelayClasp=-34, EnableReprisalSeal=-35, EnableHeartLamp=-36, EnableTinderPouch=-37, EnableMeridianSpike=-38, EnableThunderBell=-39, EnableFalterMirror=-40, EnableLastStandCharm=-41, EnableAfflictionSeal=-42, EnableSupportArtifacts=-43, EnableCriticalArtifacts=-44;
            public int kind, years, target, atBattle, atService;
            public bool accepted;
            public CheckpointAction(int command, int value, int cell)
            { kind = command; target = value; years = cell; }
            public CheckpointAction(int itemId, int count)
            {
                kind = GrantNativeItem;
                target = itemId;
                years = count;
            }
            public CheckpointAction(WorldAction action)
            {
                kind = action == null ? -1 : (int)action.Kind;
                years = action?.Years ?? 1;
                target = action?.TargetNodeId ?? 0;
            }
            public WorldAction ToAction()
            {
                switch ((WorldActionKind)kind)
                {
                    case WorldActionKind.Move: return WorldAction.Move(target, years);
                    case WorldActionKind.Cultivate: return WorldAction.Cultivate(years);
                    case WorldActionKind.Rest when years == 1: return WorldAction.Rest();
                    case WorldActionKind.Wait: return WorldAction.Wait(years);
                    case WorldActionKind.Disguise: return WorldAction.Disguise();
                    case WorldActionKind.JoinChosen: return WorldAction.JoinChosen();
                    default: throw new InvalidDataException("续玩存档包含未知行动。");
                }
            }
        }

        private static string CheckpointPath => Path.Combine(SaveDirectory, "current-run.json");
        private Checkpoint checkpoint;

#if UNITY_EDITOR
        public static void ClearSaves()
        {
            if (FindFirstObjectByType<WorldTimelineDebugView>() != null)
                throw new InvalidOperationException("请先返回标题页，再清空轮回存档。");

            string checkpointPath = CheckpointPath;
            string historyPath = SavePath;
            string suffix = ".reset-" + Guid.NewGuid().ToString("N");
            string stagedCheckpoint = checkpointPath + suffix;
            string stagedHistory = historyPath + suffix;

            // Move both files out of their active slots before deleting either one.
            bool movedCheckpoint = StageSaveForDeletion(checkpointPath, stagedCheckpoint);
            try
            {
                StageSaveForDeletion(historyPath, stagedHistory);
            }
            catch
            {
                if (movedCheckpoint) File.Move(stagedCheckpoint, checkpointPath);
                throw;
            }

            DeleteStagedSave(stagedCheckpoint);
            DeleteStagedSave(stagedHistory);
        }

        private static bool StageSaveForDeletion(string path, string stagedPath)
        {
            try
            {
                File.Move(path, stagedPath);
                return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        private static void DeleteStagedSave(string path)
        {
            try { File.Delete(path); }
            catch (DirectoryNotFoundException) { }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning($"已清空游戏使用的轮回存档，但删除暂存文件失败：{path}\n{error}");
            }
        }

        public bool TryGrantNativeItem(int itemId, int count)
        {
            if (itemId <= 0 || count < 1 || count > 99 || advancing || preparing ||
                simulation == null || simulation.State.IsEnded || !simulation.WaitingForPlayer)
                return false;

            var player = simulation.State.Actors[simulation.State.PlayerActorId];
            if (!player.IsAlive) return false;
            int firstAddedIndex = player.Items.Count;
            AddNativeItems(itemId, count);
            if (checkpoint == null) return true;

            checkpoint.actions.Add(new CheckpointAction(itemId, count));
            if (SaveCheckpoint()) return true;
            checkpoint.actions.RemoveAt(checkpoint.actions.Count - 1);
            player.Items.RemoveRange(firstAddedIndex, count);
            return false;
        }
#endif

        private void AddNativeItems(int itemId, int count)
        {
            // Negative IDs keep Item-table equipment separate from timeline rewards.
            var items = simulation.State.Actors[simulation.State.PlayerActorId].Items;
            for (int i = 0; i < count; i++) items.Add(-itemId);
        }
        public static bool IsRestoringCheckpoint { get; private set; }
        public static bool HasUnfinishedRun
        {
            get
            {
                try
                {
                    if (!File.Exists(CheckpointPath)) return false;
                    var data = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(CheckpointPath));
                    return data != null && data.version >= 1 && data.version <= Checkpoint.LatestVersion &&
                        !data.ended && !string.IsNullOrEmpty(data.histories);
                }
                catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
                { return false; }
            }
        }

        private void BeginCheckpoint()
        {
            balanceConfigVersion = GetBalanceConfigVersion();
            if (IsRestoringCheckpoint) return;
            if (returnToMenu == null)
            {
                StartBalanceLog(Guid.NewGuid().ToString("N"), false);
                return;
            }
            var histories = simulation.Histories.Select(history => new SavedLife(1, history.Record)).ToArray();
            checkpoint = new Checkpoint { round = round, item = startingCultivator.Item,
                chosenRoute = activeChosenRoute,
                balanceRunId = Guid.NewGuid().ToString("N"),
                battleRewardsFromYear = simulation.State.BattleRewardsFromYear,
                hasLifespanCutoff = true,
                histories = Convert.ToBase64String(WorldTimelineSaveStore.Encode(histories)) };
            if (Expedition != null) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableExpedition, 0, 0));
            if (Expedition?.AttritionEnabled == true) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableAttrition,0,0));
            if (Expedition?.ProgressionEnabled == true) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableMapProgression, 0, 0));
            if (Expedition?.CompanionStagesEnabled == true) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableCompanionStages, 0, 0));
            if (Expedition?.VanguardEnabled == true) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableVanguard, 0, 0));
            if (Expedition?.Equipment.Enabled == true) checkpoint.actions.Add(new CheckpointAction(
                Expedition.Equipment.Modern?CheckpointAction.EnableArtifacts:CheckpointAction.EnableTeamEquipment,0,0));
            if (Expedition?.Equipment.LinksEnabled == true) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableArtifactLinks,0,0));
            if (simulation.State.SignatureArtifacts) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableSignatureArtifacts,0,0));
            if (simulation.State.SwordTraining) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableSwordTraining,0,0));
            if (simulation.State.QuakeSpikeEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableQuakeSpike,0,0));
            if (simulation.State.RainTasselEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableRainTassel,0,0));
            if (simulation.State.RelayClaspEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableRelayClasp,0,0));
            if (simulation.State.ReprisalSealEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableReprisalSeal,0,0));
            if (simulation.State.HeartLampEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableHeartLamp,0,0));
            if (simulation.State.TinderPouchEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableTinderPouch,0,0));
            if (simulation.State.MeridianSpikeEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableMeridianSpike,0,0));
            if (simulation.State.ThunderBellEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableThunderBell,0,0));
            if (simulation.State.FalterMirrorEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableFalterMirror,0,0));
            if (simulation.State.LastStandCharmEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableLastStandCharm,0,0));
            if (simulation.State.AfflictionSealEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableAfflictionSeal,0,0));
            if (simulation.State.SupportArtifactsEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableSupportArtifacts,0,0));
            if (simulation.State.CriticalArtifactsEnabled) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableCriticalArtifacts,0,0));
            if(simulation.State.SharedClears) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableSharedClears,0,0));
            if (simulation.State.HistoricalGrowth) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableHistoricalGrowth,0,0));
            if (simulation.State.PastInvitations) checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnablePastInvitations,0,0));
            SaveCheckpoint();
            StartBalanceLog(checkpoint.balanceRunId, false);
        }

        private bool SaveCheckpoint()
        {
            if (checkpoint == null || simulation == null || IsRestoringCheckpoint) return true;
            string temporary = CheckpointPath + ".tmp";
            try
            {
                var player = simulation.State.Actors[simulation.State.PlayerActorId];
                checkpoint.selectedNode = selectedNode;
                checkpoint.guidedDiscovery = guidedDiscovery;
                checkpoint.openingMap = openingMap;
                checkpoint.openingPrompt = openingPrompt;
                checkpoint.openingChoice = openingChoice;
                checkpoint.year = simulation.State.Year;
                checkpoint.regionalGuardiansFromYear = simulation.State.RegionalGuardiansFromYear;
                checkpoint.outerGuardiansFromYear = simulation.State.OuterGuardiansFromYear;
                checkpoint.health = player.Health;
                checkpoint.attack = player.Attack;
                checkpoint.level = player.Level;
                checkpoint.spiritStones = player.LegacySpiritStones;
                checkpoint.ended = simulation.State.IsEnded;
                checkpoint.storyChoices = simulation.RecordPlayerLife().StoryChoices.Select(choice => new CheckpointStoryChoice(choice)).ToList();
                Directory.CreateDirectory(Path.GetDirectoryName(CheckpointPath));
                File.WriteAllText(temporary, JsonUtility.ToJson(checkpoint));
                if (File.Exists(CheckpointPath)) File.Replace(temporary, CheckpointPath, null);
                else File.Move(temporary, CheckpointPath);
                RecordCollection();
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                saveIssue = TimelineText("Timeline_UI_127") + error.Message;
                Debug.LogError(saveIssue);
                return false;
            }
        }

        public bool ResumeUnfinishedRun()
        {
            try
            {
                if (!HasUnfinishedRun) throw new InvalidDataException("没有可继续的轮回存档。");
                var data = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(CheckpointPath));
                var histories = WorldTimelineSaveStore.Decode(Convert.FromBase64String(data.histories));
                if (data.actions == null || data.actions.Count > 100000 || data.round < 1)
                    throw new InvalidDataException("续玩存档行动数量或轮次无效。");
                if (data.version >= 29 && data.chosenRoute == null)
                    throw new InvalidDataException("续玩存档缺少无名客路线。");
                startingCultivator = roster.Single(entry => entry.Item == data.item);
                round = data.round;
                stagedRoutes = data.version >= 57;
                guidedDiscovery = data.guidedDiscovery;
                openingChoice = data.openingChoice;
                IsRestoringCheckpoint = true;
                NewRound(histories.Select(entry => entry.Record), data.version >= 2,
                    data.storyChoices?.Select(choice => choice.ToChoice()), expandedExploration: data.version >= 3,
                    storyCombatSkills: data.version >= 4, bleedRewardRoute: data.version >= 5,
                    wreckBeastEncounter: data.version >= 6, swordRecall: data.version >= 7,
                    techniqueCombat: data.version >= 8, swordTrial: data.version >= 9, lightningRoute: data.version >= 10, battleExperience: data.version >= 11, lightningTrials: data.version >= 12, balancedLightningTrials: data.version >= 13, merchants: data.version >= 14,
                    linxiRoute: data.version >= 16, closeLinxiFormation: data.version >= 17,
                    eastRoute: data.version >= 18, northRoute: data.version >= 19, westRoute: data.version >= 20,
                    snowRoute: data.version >= 21, gorgeRoute: data.version >= 22, tideRoute: data.version >= 23,
                    blackfangPounce: data.version >= 24, whitefoamSalvage: data.version >= 25, gorgeBrace: data.version >= 26,
                    ashridgeStory: data.version >= 27, contractPostRoute: data.version >= 28,
                    useConfiguredChosenRoute: data.version >= 29, chosenRouteOverride: data.chosenRoute,
                    useOpeningMap: data.openingMap, characterStories: data.version >= 31, regionalExploration: data.version >= 32,
                    sluiceCampaign: data.version >= 33, northRelay: data.version >= 34, legacyAttack: data.version < 35,
                    caveRegions: data.version >= 36, contractStory: data.version >= 37, northRelaySpring: data.version >= 38,
                    sluiceDrainsBoss: data.version >= 39, saltChannel: data.version >= 40, expandedMap: data.version >= 41,
                    expandedMapEncounters: data.version >= 42, deferHistoryFormation: data.version >= 51,
                    practiceKiln: data.version >= 53, bleedTrials: data.version >= 54,
                    ancientSwordWaitsForHit: data.version >= 55, shortAncientSwordOpening: data.version >= 56);
                int cutoff = data.hasLifespanCutoff ? data.lifespanThroughYear : data.year;
                if (cutoff < 0 || cutoff > data.year)
                    throw new InvalidDataException("续玩存档的旧寿元结算截止年份无效。");
                simulation.State.LegacyLifespanDeathThroughYear = cutoff;
                simulation.State.BattleRewardsFromYear = data.battleRewardsFromYear > 0
                    ? data.battleRewardsFromYear : int.MaxValue;
                simulation.State.RegionalGuardiansFromYear = data.regionalGuardiansFromYear > 0
                    ? data.regionalGuardiansFromYear : int.MaxValue;
                simulation.State.OuterGuardiansFromYear = data.outerGuardiansFromYear > 0
                    ? data.outerGuardiansFromYear : int.MaxValue;
                int tradeCutoff = data.hasLegacyTradeCutoff ? data.legacyTradeThroughYear
                    : data.version < 15 ? data.year : -1;
                if (tradeCutoff < -1 || tradeCutoff > data.year)
                    throw new InvalidDataException("续玩存档的旧交易结算截止年份无效。");
                simulation.State.LegacyTradeThroughYear = tradeCutoff;
                data.hasLegacyTradeCutoff = true;
                data.legacyTradeThroughYear = tradeCutoff;
                data.hasLifespanCutoff = true;
                data.lifespanThroughYear = cutoff;
                foreach (var step in data.actions)
                {
                    if (step.kind == CheckpointAction.PastInvitationChoice)
                    {
                        if (step.years <= simulation.State.Year || step.atBattle != 0 || step.atService != 0)
                            throw new InvalidDataException("前世相遇记录无效。");
                        replayInvitations.Enqueue(step);
                        continue;
                    }
                    if (step.kind <= CheckpointAction.EnableExpedition && (step.kind >= CheckpointAction.EnablePastInvitations || step.kind == CheckpointAction.EnableSignatureArtifacts || step.kind == CheckpointAction.EnableSwordTraining || step.kind == CheckpointAction.EnableQuakeSpike || step.kind == CheckpointAction.EnableRainTassel || step.kind == CheckpointAction.EnableRelayClasp || step.kind == CheckpointAction.EnableReprisalSeal || step.kind == CheckpointAction.EnableHeartLamp || step.kind == CheckpointAction.EnableTinderPouch || step.kind == CheckpointAction.EnableMeridianSpike || step.kind == CheckpointAction.EnableThunderBell || step.kind == CheckpointAction.EnableFalterMirror || step.kind == CheckpointAction.EnableLastStandCharm || step.kind == CheckpointAction.EnableAfflictionSeal || step.kind == CheckpointAction.EnableSupportArtifacts || step.kind == CheckpointAction.EnableCriticalArtifacts))
                    {
                        if(step.atService!=0)
                        {
                            if(step.atService<=equipmentServiceNumber || step.atBattle!=0 ||
                                step.kind is not (CheckpointAction.BuyArtifact or CheckpointAction.UpgradeArtifact or CheckpointAction.EventUpgrade))
                                throw new InvalidDataException("商人或事件升级顺序无效。");
                            replayEquipmentServices.Add(step);continue;
                        }
                        if (step.atBattle != 0)
                        {
                            if (step.atBattle <= expeditionBattleNumber || step.kind is not (CheckpointAction.PlaceCompanion or CheckpointAction.BenchCompanion or CheckpointAction.ClaimTeamReward or CheckpointAction.SelectSuccessor))
                                throw new InvalidDataException("续玩存档的战前布阵顺序无效。");
                            if(step.kind==CheckpointAction.SelectSuccessor)replaySuccessors.Add(step);
                            else if(step.kind==CheckpointAction.ClaimTeamReward)replayTeamRewards.Add(step);
                            else replayFormations.Add(step);
                            continue;
                        }
                        if (!ApplyExpeditionCommand(step.kind, step.target, step.years))
                            throw new InvalidDataException("续玩存档的招募或布阵记录无效。");
                        continue;
                    }
                    if (step.kind == CheckpointAction.GrantNativeItem)
                    {
                        if (step.target <= 0 || step.years < 1 || step.years > 99)
                            throw new InvalidDataException("续玩存档的调试装备记录无效。");
                        AddNativeItems(step.target, step.years);
                        continue;
                    }
                    if (step.kind < -1) throw new InvalidDataException("续玩存档行动无效。");
                    DrainRestoreRoutine(step.kind == -1 ? simulation.AdvanceOneYearRoutine()
                        : simulation.SubmitPlayerActionRoutine(step.ToAction()));
                }
                var player = simulation.State.Actors[simulation.State.PlayerActorId];
                if (replayInvitations.Count!=0 || replaySuccessors.Count!=0 || replayEquipmentServices.Count!=0 || replayFormations.Count != 0 || replayTeamRewards.Count!=0 || simulation.State.Year != data.year || player.Health != data.health
                    || player.Attack != data.attack || player.Level != data.level
                    || player.LegacySpiritStones != data.spiritStones || simulation.State.IsEnded)
                    throw new InvalidDataException("存档演算结果不一致，可能是配置或战斗规则已变化。");
                if (data.battleRewardsFromYear <= 0)
                {
                    // Old battles replay with their original rules. Future battles pay the new rewards.
                    data.battleRewardsFromYear = checked(data.year + 1);
                    simulation.State.BattleRewardsFromYear = data.battleRewardsFromYear;
                }
                selectedNode = data.selectedNode;
                if (data.regionalGuardiansFromYear <= 0)
                    simulation.State.RegionalGuardiansFromYear = checked(data.year + 1);
                if (data.outerGuardiansFromYear <= 0)
                    simulation.State.OuterGuardiansFromYear = checked(data.year + 1);
                if (!simulation.State.Nodes.ContainsKey(selectedNode)) throw new InvalidDataException("存档节点无效。");
                archived = savedLives.Any(entry => entry.Round == round);
                checkpoint = data;
                if (Expedition == null && player.IsAlive && player.NodeId.HasValue && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableExpedition();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableExpedition, 0, 0));
                }
                if (Expedition != null && !Expedition.ProgressionEnabled && !simulation.State.PlayerControlEnded)
                {
                    EnableMapProgression();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableMapProgression, 0, 0));
                }
                openingPrompt = data.openingMap && data.openingPrompt;
                if (Expedition != null && !Expedition.CompanionStagesEnabled && !simulation.State.PlayerControlEnded)
                {
                    EnableCompanionStages();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableCompanionStages, 0, 0));
                }
                if (!Guid.TryParseExact(data.balanceRunId, "N", out _))
                {
                    data.balanceRunId = Guid.NewGuid().ToString("N");
                }
                if (Expedition != null && !Expedition.VanguardEnabled && !simulation.State.PlayerControlEnded)
                {
                    EnableVanguard();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableVanguard, 0, 0));
                }
                if(Expedition!=null && !Expedition.Equipment.Modern && !simulation.State.PlayerControlEnded)
                {
                    Expedition.Equipment.EnableArtifacts();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableArtifacts,0,0));
                }
                if (Expedition != null && !Expedition.AttritionEnabled && !simulation.State.PlayerControlEnded)
                {
                    Expedition.EnableAttrition();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableAttrition,0,0));
                }
                if (Expedition != null && !Expedition.Equipment.LinksEnabled && !simulation.State.PlayerControlEnded)
                {
                    Expedition.Equipment.EnableLinks();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableArtifactLinks,0,0));
                }
                if (!simulation.State.SignatureArtifacts && Expedition != null && !simulation.State.PlayerControlEnded)
                {
                    Expedition.EnableSignatureArtifacts();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableSignatureArtifacts,0,0));
                }
                if (!simulation.State.SwordTraining && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableSwordTraining();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableSwordTraining,0,0));
                }
                if (!simulation.State.QuakeSpikeEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableQuakeSpike();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableQuakeSpike,0,0));
                }
                if (!simulation.State.RainTasselEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableRainTassel();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableRainTassel,0,0));
                }
                if (!simulation.State.RelayClaspEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableRelayClasp();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableRelayClasp,0,0));
                }
                if (!simulation.State.ReprisalSealEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableReprisalSeal();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableReprisalSeal,0,0));
                }
                if (!simulation.State.HeartLampEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableHeartLamp();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableHeartLamp,0,0));
                }
                if (!simulation.State.TinderPouchEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableTinderPouch();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableTinderPouch,0,0));
                }
                if (!simulation.State.MeridianSpikeEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableMeridianSpike();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableMeridianSpike,0,0));
                }
                if (!simulation.State.ThunderBellEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableThunderBell();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableThunderBell,0,0));
                }
                if (!simulation.State.FalterMirrorEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableFalterMirror();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableFalterMirror,0,0));
                }
                if (!simulation.State.LastStandCharmEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableLastStandCharm();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableLastStandCharm,0,0));
                }
                if (!simulation.State.AfflictionSealEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableAfflictionSeal();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableAfflictionSeal,0,0));
                }
                if (!simulation.State.SupportArtifactsEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableSupportArtifacts();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableSupportArtifacts,0,0));
                }
                if (!simulation.State.CriticalArtifactsEnabled && !simulation.State.PlayerControlEnded)
                {
                    simulation.State.EnableCriticalArtifacts();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableCriticalArtifacts,0,0));
                }
                if (!simulation.State.SharedClears && !simulation.State.PlayerControlEnded)
                {
                    simulation.EnableSharedClears();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableSharedClears,0,0));
                }
                if (!simulation.State.HistoricalGrowth && !simulation.State.PlayerControlEnded)
                {
                    simulation.EnableHistoricalGrowth();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnableHistoricalGrowth,0,0));
                }
                if (!simulation.State.PastInvitations && !simulation.State.PlayerControlEnded)
                {
                    simulation.EnablePastInvitations();
                    checkpoint.actions.Add(new CheckpointAction(CheckpointAction.EnablePastInvitations,0,0));
                }
                preparing = false;
                if (simulation.Stories != null) simulation.Stories.InteractivePlayer = true;
                InitializeBoard();
                ShowOpeningMap();
                OpenMapHud?.Invoke(this);
                if (simulation.Stories != null && !UsesStoryHud) OpenStoryHud?.Invoke(this);
                message = TimelineText("Timeline_UI_Resumed", simulation.State.Year);
                RecordCollection();
                StartBalanceLog(data.balanceRunId, true);
                return true;
            }
            catch (Exception error)
            {
                preparing = true;
                simulation = null;
                saveIssue = TimelineText("Timeline_UI_128") + error.Message;
                Debug.LogError(saveIssue);
                return false;
            }
            finally { IsRestoringCheckpoint = false; }
        }

        private static void DrainRestoreRoutine(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    var current = stack.Peek();
                    if (!current.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current.Current is IEnumerator nested) stack.Push(nested);
                    else if (current.Current != null) throw new InvalidOperationException("存档恢复出现前台等待。");
                }
            }
            finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && !advancing && !preparing) SaveCheckpoint();
        }

        private void OnApplicationQuit()
        {
            if (!advancing && !preparing) SaveCheckpoint();
        }
    }
}
