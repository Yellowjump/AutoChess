using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldTimeline;

namespace WorldTimelineDebug.Editor
{
    public static class WorldTimelineDebugMenu
    {
        public const string ScenePath = "Assets/GameMain/Scenes/WorldTimelineDebug.unity";

        [MenuItem("Tools/世界时间线/打开调试场景")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("请先停止 Play，再打开调试场景。"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/世界时间线/打开并运行调试场景")]
        public static void OpenAndPlay()
        {
            Open();
            if (EditorSceneManager.GetActiveScene().path == ScenePath) EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/世界时间线/打开养成调参面板")]
        public static void OpenBalanceDashboard()
        {
            try
            {
                string tools = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Tools"));
                string template = Path.Combine(tools, "WorldTimelineBalance.html");
                string html = File.ReadAllText(template);
                string logDirectory = WorldTimelineDebugView.BalanceLogDirectory;
                var payload = new StringBuilder();
                if (Directory.Exists(logDirectory))
                {
                    foreach (string path in Directory.EnumerateFiles(logDirectory, "*.jsonl").OrderBy(path => path, StringComparer.Ordinal))
                    {
                        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var reader = new StreamReader(stream);
                        payload.AppendLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(reader.ReadToEnd())));
                    }
                }

                string baseUri = new Uri(tools + Path.DirectorySeparatorChar).AbsoluteUri;
                html = html.Replace("<!-- unity-dashboard-base -->", "<base href=\"" + baseUri + "\">")
                    .Replace("<!-- unity-dashboard-logs -->", "<script id=\"unity-logs\" type=\"text/plain\">" + payload + "</script>");
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "WorldTimelineBalance.html"));
                File.WriteAllText(output, html, new UTF8Encoding(false));
                Application.OpenURL(new Uri(output).AbsoluteUri);
            }
            catch (IOException error)
            {
                Debug.LogError("无法打开养成调参面板：" + error.Message);
            }
            catch (UnauthorizedAccessException error)
            {
                Debug.LogError("无法读取养成调参面板或日志：" + error.Message);
            }
        }

        // CLI validation: imports/compiles the real project, opens the serialized scene,
        // then verifies both default relay and no-relay outcomes in the Unity runtime.
        public static void ValidateScene()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath);
                WorldTimelineDebugView view = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var candidate = root.GetComponent<WorldTimelineDebugView>();
                    if (candidate != null) view = candidate;
                }
                if (view == null || UnityEngine.Object.FindFirstObjectByType<Camera>() == null)
                    throw new Exception("Debug scene root or camera is missing.");
                var growth = CultivationConfig.ParseCsv(File.ReadAllText(Path.Combine(Application.dataPath,
                    "../../Tests/WorldTimeline/Fixtures/CultivationLevels.csv")));
                var simulation = HexTimelineDemoFactory.Create(cultivation: growth);
                simulation.AddHistory(HexTimelineDemoFactory.CreateRelayRecord(30, growth), 10);
                simulation.AddHistory(HexTimelineDemoFactory.CreateRelayRecord(60, growth), 11);
                simulation.AddHistory(HexTimelineDemoFactory.CreateRelayRecord(90, growth), 12);
                simulation.SubmitPlayerAction(WorldAction.Cultivate(120));
                while (!simulation.State.IsEnded && simulation.State.Year < 150) simulation.AdvanceOneYear();
                if (simulation.State.Outcome != RunOutcome.Cleared || simulation.State.Year != 100 || simulation.State.Nodes.Count != 331)
                    throw new Exception("Hex relay fixture failed to clear at year 100.");
                var empty = HexTimelineDemoFactory.Create(cultivation: growth);
                empty.SubmitPlayerAction(WorldAction.Wait(50));
                if (empty.State.Outcome != RunOutcome.WorldDestroyed || empty.State.Year != 35)
                    throw new Exception("No-relay fixture should expire at year 35.");
                var traveller = HexTimelineDemoFactory.Create(cultivation: growth);
                traveller.AssignAction(1, WorldAction.Move(HexWorldMap.Id(1, -10), 10));
                for (int i = 0; i < 5; i++) traveller.AdvanceOneYear();
                if (Math.Abs(ActorMapProjection.Position(traveller.State, traveller.State.Actors[1]).Q - .5) > .001)
                    throw new Exception("Moving actor map projection missing.");
                string report = "PASS: Unity compiled debug assemblies, loaded scene, camera and growth CSV, 331 hex cells, moving actors project correctly, additive growth active, relay cleared at year 100, no-relay expired at year 35.\n" +
                    "GUI rendering and mouse interaction still require a Play Mode visual check.\n";
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/WorldTimelineDebugValidation.txt", report);
                Debug.Log(report);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
    }
}
