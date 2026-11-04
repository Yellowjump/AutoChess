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
            public int version = 1, round, item, selectedNode, year, health, attack, level;
            public bool ended;
            public string histories;
            public List<CheckpointAction> actions = new List<CheckpointAction>();
        }

        [Serializable]
        private sealed class CheckpointAction
        {
            public int kind, years, target;
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

        private static string CheckpointPath => Path.Combine(Application.persistentDataPath, "WorldTimeline", "current-run.json");
        private Checkpoint checkpoint;
        public static bool IsRestoringCheckpoint { get; private set; }
        public static bool HasUnfinishedRun
        {
            get
            {
                try
                {
                    if (!File.Exists(CheckpointPath)) return false;
                    var data = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(CheckpointPath));
                    return data != null && data.version == 1 && !data.ended && !string.IsNullOrEmpty(data.histories);
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
                checkpoint.ended = simulation.State.IsEnded;
                Directory.CreateDirectory(Path.GetDirectoryName(CheckpointPath));
                File.WriteAllText(temporary, JsonUtility.ToJson(checkpoint));
                if (File.Exists(CheckpointPath)) File.Replace(temporary, CheckpointPath, null);
                else File.Move(temporary, CheckpointPath);
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                saveIssue = "当前局保存失败：" + error.Message;
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
                NewRound(histories.Select(entry => entry.Record));
                foreach (var step in data.actions)
                {
                    if (step.kind < -1) throw new InvalidDataException("续玩存档行动无效。");
                    DrainRestoreRoutine(step.kind == -1 ? simulation.AdvanceOneYearRoutine()
                        : simulation.SubmitPlayerActionRoutine(step.ToAction()));
                }
                var player = simulation.State.Actors[simulation.State.PlayerActorId];
                if (simulation.State.Year != data.year || player.Health != data.health
                    || player.Attack != data.attack || player.Level != data.level || simulation.State.IsEnded)
                    throw new InvalidDataException("存档演算结果不一致，可能是配置或战斗规则已变化。");
                selectedNode = data.selectedNode;
                if (!simulation.State.Nodes.ContainsKey(selectedNode)) throw new InvalidDataException("存档节点无效。");
                archived = savedLives.Any(entry => entry.Round == round);
                checkpoint = data;
                preparing = false;
                InitializeBoard();
                OpenMapHud?.Invoke(this);
                message = "已继续上一局，第 " + simulation.State.Year + " 年。";
                return true;
            }
            catch (Exception error)
            {
                preparing = true;
                simulation = null;
                saveIssue = "继续轮回失败：" + error.Message;
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
