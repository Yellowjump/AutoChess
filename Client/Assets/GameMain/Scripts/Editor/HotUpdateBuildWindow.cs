using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

internal sealed class HotUpdateBuildWindow : EditorWindow
{
    [Serializable]
    private sealed class VersionInfo
    {
        public int internalResourceVersion;
    }

    private const string ServerKey = "Zizouqi.HotUpdate.Server";
    private const string UserKey = "Zizouqi.HotUpdate.User";
    private const string KeyPathKey = "Zizouqi.HotUpdate.KeyPath";
    private const string PortKey = "Zizouqi.HotUpdate.Port";
    private const string PlayerDirectoryKeyPrefix = "Zizouqi.HotUpdate.PlayerDirectory.";

    private static string s_Status = "Ready";
    private static bool s_Building;
    private static int s_CleanupVersion;
    private static int s_PreviewedCleanupVersion;

    [MenuItem("HybridCLR/Hot Update Build Window", priority = 299)]
    private static void Open()
    {
        GetWindow<HotUpdateBuildWindow>("Hot Update Build");
    }

    private void OnGUI()
    {
        string project = Directory.GetParent(Application.dataPath).FullName;
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string platform = target.ToString();
        string playerDirectory = GetPlayerDirectory(project, target);
        EditorGUILayout.LabelField("Target", platform);
        EditorGUILayout.LabelField("App Version", PlayerSettings.bundleVersion);
        EditorGUILayout.LabelField("Built-in Resource Version",
            ReadResourceVersion(Path.Combine(Application.streamingAssetsPath, "version.json")));
        EditorGUILayout.LabelField("Hot-update Resource Version",
            ReadResourceVersion(Path.Combine(project, "Builds", "HotUpdateServer", platform, "version.json")));
        EditorGUILayout.LabelField("Player Folder");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.SelectableLabel(playerDirectory,
            GUILayout.Height(EditorGUIUtility.singleLineHeight));
        if (GUILayout.Button("选择文件夹", GUILayout.Width(90f)))
        {
            string selected = EditorUtility.OpenFolderPanel("选择 EXE 输出文件夹", playerDirectory, "");
            if (!string.IsNullOrEmpty(selected))
            {
                EditorPrefs.SetString(PlayerDirectoryKeyPrefix + platform, selected);
                playerDirectory = selected;
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Player File");
        EditorGUILayout.SelectableLabel(GetPlayerOutputPath(playerDirectory, target),
            GUILayout.Height(EditorGUIUtility.singleLineHeight));
        EditorGUILayout.LabelField("Upload", s_Status);
        EditorGUILayout.Space(8f);

        bool busy = s_Building;
        using (new EditorGUI.DisabledScope(busy))
        {
            EditorGUI.BeginChangeCheck();
            string server = EditorGUILayout.TextField("VPS", EditorPrefs.GetString(ServerKey, "64.176.56.225"));
            string user = EditorGUILayout.TextField("SSH User", EditorPrefs.GetString(UserKey, "root"));
            string keyPath = EditorGUILayout.TextField("SSH Key", EditorPrefs.GetString(KeyPathKey, DefaultKeyPath));
            int port = EditorGUILayout.IntField("SSH Port", EditorPrefs.GetInt(PortKey, 22));
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(ServerKey, server);
                EditorPrefs.SetString(UserKey, user);
                EditorPrefs.SetString(KeyPathKey, keyPath);
                EditorPrefs.SetInt(PortKey, port);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Builds the Player and built-in AB, then publishes matching hot-update resources and uploads them to the VPS.",
                MessageType.Info);
            if (GUILayout.Button("构建全包并上传 VPS", GUILayout.Height(38f)))
            {
                Run(true);
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.HelpBox(
                "Rebuilds only the hot-update DLL, AB and version files, then uploads them to the VPS. The Player and StreamingAssets stay unchanged.",
                MessageType.Info);
            if (GUILayout.Button("构建热更包并上传 VPS", GUILayout.Height(38f)))
            {
                Run(false);
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.HelpBox("Uploads the existing local publication and creates its version folder on the VPS without rebuilding DLL or AB.", MessageType.Info);
            if (GUILayout.Button("同步当前版本目录到 VPS", GUILayout.Height(30f)))
            {
                SyncCurrentRelease();
            }

            EditorGUILayout.Space(12f);
            s_CleanupVersion = EditorGUILayout.IntField("要删除的版本文件夹", s_CleanupVersion);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(s_CleanupVersion < 1))
            {
                if (GUILayout.Button("预览清理")) RunCleanup(false);
            }
            using (new EditorGUI.DisabledScope(s_CleanupVersion < 1 || s_CleanupVersion != s_PreviewedCleanupVersion))
            {
                if (GUILayout.Button("删除此版本及无用文件")) RunCleanup(true);
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private static void SyncCurrentRelease()
    {
        try
        {
            ValidateUploadSettings();
            s_Building = true;
            PrepareLocalHotUpdate.ExportReadableHotUpdateManifest();
            bool uploaded = UploadNow();
            s_Status = uploaded ? "Current release folder synced" : "VPS already matches; upload skipped";
        }
        catch (Exception exception)
        {
            s_Status = "Current release sync failed";
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Sync failed", exception.Message, "OK");
        }
        finally
        {
            s_Building = false;
        }
    }

    private static void RunCleanup(bool apply)
    {
        try
        {
            ValidateUploadSettings();
            s_Building = true;
            string project = Directory.GetParent(Application.dataPath).FullName;
            string script = Path.Combine(project, "Tools", "Clean-OldHotUpdateFiles.ps1");
            if (!File.Exists(script)) throw new FileNotFoundException("The cleanup script is missing.", script);
            string logPath = Path.Combine(project, "Builds", "cleanup-hot-update.log");
            StringBuilder output = new StringBuilder();
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                    " -Server " + Quote(EditorPrefs.GetString(ServerKey, "64.176.56.225")) +
                    " -User " + Quote(EditorPrefs.GetString(UserKey, "root")) +
                    " -KeyPath " + Quote(EditorPrefs.GetString(KeyPathKey, DefaultKeyPath)) +
                    " -Port " + EditorPrefs.GetInt(PortKey, 22) +
                    " -Platform " + Quote(EditorUserBuildSettings.activeBuildTarget.ToString()) +
                    " -RemoveVersions " + s_CleanupVersion + (apply ? " -Apply" : ""),
                WorkingDirectory = project,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = new Process { StartInfo = start })
            {
                process.OutputDataReceived += (sender, e) => CollectOutput(output, e);
                process.ErrorDataReceived += (sender, e) => CollectOutput(output, e);
                if (!process.Start()) throw new InvalidOperationException("Could not start the cleanup script.");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                string log;
                lock (output) log = output.ToString();
                File.WriteAllText(logPath, log);
                if (process.ExitCode != 0) throw new InvalidOperationException(log);
                if (apply)
                {
                    s_PreviewedCleanupVersion = 0;
                    s_Status = "Old release removed";
                }
                else
                {
                    s_PreviewedCleanupVersion = s_CleanupVersion;
                    s_Status = "Cleanup preview ready";
                }
                EditorUtility.DisplayDialog(apply ? "Cleanup complete" : "Cleanup preview", log, "OK");
            }
        }
        catch (Exception exception)
        {
            s_PreviewedCleanupVersion = 0;
            s_Status = "Cleanup failed";
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Cleanup failed", exception.Message, "OK");
        }
        finally
        {
            s_Building = false;
        }
    }

    private static string DefaultKeyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "zizouqi_deploy_ed25519");

    private static string ReadResourceVersion(string path)
    {
        if (!File.Exists(path)) return "Not built";
        try
        {
            VersionInfo info = JsonUtility.FromJson<VersionInfo>(File.ReadAllText(path));
            return info == null ? "Invalid version file" : info.internalResourceVersion.ToString();
        }
        catch (Exception)
        {
            return "Invalid version file";
        }
    }

    private static string GetPlayerDirectory(string project, BuildTarget target)
    {
        return EditorPrefs.GetString(PlayerDirectoryKeyPrefix + target,
            Path.Combine(project, "Builds", target.ToString()));
    }

    private static string GetPlayerOutputPath(string directory, BuildTarget target)
    {
        string extension = target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64
            ? ".exe" : target == BuildTarget.Android ? ".apk" : target == BuildTarget.StandaloneOSX ? ".app" : "";
        return Path.Combine(directory, PlayerSettings.productName + extension);
    }

    private static void Run(bool buildPlayer)
    {
        try
        {
            ValidateUploadSettings();
            s_Building = true;
            s_Status = buildPlayer ? "Building Player and resources" : "Building hot-update resources";
            if (buildPlayer)
            {
                string project = Directory.GetParent(Application.dataPath).FullName;
                PrepareLocalHotUpdate.BuildLocalPlayerToDirectory(
                    GetPlayerDirectory(project, EditorUserBuildSettings.activeBuildTarget));
            }
            PrepareLocalHotUpdate.PublishLocalHotUpdateFilesIfChanged();
            bool uploaded = UploadNow();
            s_Status = uploaded ? "Build and VPS sync completed" : "VPS already matches; upload skipped";
        }
        catch (Exception exception)
        {
            s_Status = "Build or upload failed";
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Build failed", exception.Message, "OK");
        }
        finally
        {
            s_Building = false;
        }
    }

    private static void ValidateUploadSettings()
    {
        if (Application.platform != RuntimePlatform.WindowsEditor)
            throw new InvalidOperationException("The VPS upload script currently requires Windows PowerShell.");

        string server = EditorPrefs.GetString(ServerKey, "64.176.56.225");
        string user = EditorPrefs.GetString(UserKey, "root");
        string keyPath = EditorPrefs.GetString(KeyPathKey, DefaultKeyPath);
        int port = EditorPrefs.GetInt(PortKey, 22);
        if (!Regex.IsMatch(server, "^[A-Za-z0-9.-]+$") || !Regex.IsMatch(user, "^[A-Za-z_][A-Za-z0-9_-]*$"))
            throw new InvalidOperationException("VPS or SSH user contains unsupported characters.");
        if (port < 1 || port > 65535 || !File.Exists(keyPath))
            throw new InvalidOperationException("Check the SSH port and key file before building.");
        if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Tools", "Publish-HotUpdate.ps1")))
            throw new FileNotFoundException("The VPS upload script is missing.");
    }

    private static bool UploadNow()
    {
        string project = Directory.GetParent(Application.dataPath).FullName;
        string script = Path.Combine(project, "Tools", "Publish-HotUpdate.ps1");
        string server = EditorPrefs.GetString(ServerKey, "64.176.56.225");
        string user = EditorPrefs.GetString(UserKey, "root");
        string keyPath = EditorPrefs.GetString(KeyPathKey, DefaultKeyPath);
        int port = EditorPrefs.GetInt(PortKey, 22);
        string uploadLogPath = Path.Combine(project, "Builds", "upload-hot-update.log");
        StringBuilder output = new StringBuilder();

        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                " -Mode UploadOnly -Server " + Quote(server) + " -User " + Quote(user) +
                " -KeyPath " + Quote(keyPath) + " -Port " + port +
                " -Platform " + Quote(EditorUserBuildSettings.activeBuildTarget.ToString()),
            WorkingDirectory = project,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        Process process = new Process { StartInfo = start };
        process.OutputDataReceived += (sender, e) => CollectOutput(output, e);
        process.ErrorDataReceived += (sender, e) => CollectOutput(output, e);
        try
        {
            s_Status = "Uploading resources to VPS";
            EditorUtility.DisplayProgressBar("Hot Update", "Uploading resources to VPS...", 0.75f);
            if (!process.Start()) throw new InvalidOperationException("Could not start the VPS uploader.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            string log;
            lock (output) log = output.ToString();
            File.WriteAllText(uploadLogPath, log);
            if (process.ExitCode != 0)
                throw new InvalidOperationException("VPS upload failed. See " + uploadLogPath + "\n" + log);
            bool uploaded = log.IndexOf("skipped upload.", StringComparison.OrdinalIgnoreCase) < 0;
            Debug.Log((uploaded ? "Hot-update files synced to VPS. " : "VPS already matches. ") + log.Trim());
            return uploaded;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            process.Dispose();
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static void CollectOutput(StringBuilder output, DataReceivedEventArgs e)
    {
        if (e.Data == null) return;
        lock (output) output.AppendLine(e.Data);
    }
}
