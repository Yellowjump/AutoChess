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
            public int version = 15, round, item, selectedNode, year, health, attack, level;
            public bool ended;
            public bool hasLifespanCutoff;
            public int lifespanThroughYear;
            public int battleRewardsFromYear;
            public long spiritStones;
            public bool hasLegacyTradeCutoff;
            public int legacyTradeThroughYear;
            public string histories;
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
            public int kind, years, target;
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
                    return data != null && data.version >= 1 && data.version <= 15 && !data.ended && !string.IsNullOrEmpty(data.histories);
                }
                catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
                { return false; }
            }
        }

        private void BeginCheckpoint()
        {
            if (returnToMenu == null || IsRestoringCheckpoint) return;
            var histories = library.Where(entry => entry.Selected).Take(10)
                .Select(entry => new SavedLife(1, entry.Record)).ToArray();
            checkpoint = new Checkpoint { round = round, item = startingCultivator.Item,
                battleRewardsFromYear = simulation.State.BattleRewardsFromYear,
                hasLifespanCutoff = true,
                histories = Convert.ToBase64String(WorldTimelineSaveStore.Encode(histories)) };
            SaveCheckpoint();
        }

        private bool SaveCheckpoint()
        {
            if (checkpoint == null || simulation == null || IsRestoringCheckpoint) return true;
            string temporary = CheckpointPath + ".tmp";
            try
            {
                var player = simulation.State.Actors[simulation.State.PlayerActorId];
                checkpoint.selectedNode = selectedNode;
                checkpoint.year = simulation.State.Year;
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
                startingCultivator = roster.Single(entry => entry.Item == data.item);
                round = data.round;
                IsRestoringCheckpoint = true;
                NewRound(histories.Select(entry => entry.Record), data.version >= 2,
                    data.storyChoices?.Select(choice => choice.ToChoice()), expandedExploration: data.version >= 3,
                    storyCombatSkills: data.version >= 4, bleedRewardRoute: data.version >= 5,
                    wreckBeastEncounter: data.version >= 6, swordRecall: data.version >= 7,
                    techniqueCombat: data.version >= 8, swordTrial: data.version >= 9, lightningRoute: data.version >= 10, battleExperience: data.version >= 11, lightningTrials: data.version >= 12, balancedLightningTrials: data.version >= 13, merchants: data.version >= 14);
                int cutoff = data.hasLifespanCutoff ? data.lifespanThroughYear : data.year;
                if (cutoff < 0 || cutoff > data.year)
                    throw new InvalidDataException("续玩存档的旧寿元结算截止年份无效。");
                simulation.State.LegacyLifespanDeathThroughYear = cutoff;
                simulation.State.BattleRewardsFromYear = data.battleRewardsFromYear > 0
                    ? data.battleRewardsFromYear : int.MaxValue;
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
                if (simulation.State.Year != data.year || player.Health != data.health
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
                if (!simulation.State.Nodes.ContainsKey(selectedNode)) throw new InvalidDataException("存档节点无效。");
                archived = savedLives.Any(entry => entry.Round == round);
                checkpoint = data;
                preparing = false;
                if (simulation.Stories != null) simulation.Stories.InteractivePlayer = true;
                InitializeBoard();
                OpenMapHud?.Invoke(this);
                if (simulation.Stories != null && !UsesStoryHud) OpenStoryHud?.Invoke(this);
                message = TimelineText("Timeline_UI_Resumed", simulation.State.Year);
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
