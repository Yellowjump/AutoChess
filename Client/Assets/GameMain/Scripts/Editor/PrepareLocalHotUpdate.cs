using System.IO;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using HybridCLR.Editor.Commands;
using GameFramework.Resource;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGameFramework.Editor.ResourceTools;
using UnityGameFramework.Runtime;
using Zizouqi.Bootstrap;

public static class PrepareLocalHotUpdate
{
    private const string BootstrapScenePath = "Assets/GameMain/Scenes/bootstrap.unity";
    private const string GameScenePath = "Assets/GameMain/Scenes/qipan.unity";
    private const string HotAssemblyName = "Assembly-CSharp.dll";
    private const string AotCompatibilityFile = "aot-compatibility.txt";
    private const string HotAssetDirectory = "Assets/GameMain/HotUpdateAssets";
    private static string s_LastPackageDirectory;

    [Serializable]
    private sealed class PublishVersionInfo
    {
        public int schemaVersion;
        public string appVersion;
        public string aotCompatibility;
        public int internalResourceVersion;
    }

    [Serializable]
    private sealed class ReadableManifest
    {
        public int internalResourceVersion;
        public string appVersion;
        public ManifestResource[] resources;
    }

    [Serializable]
    private sealed class ManifestResource
    {
        public string resourceName;
        public string fileName;
        public int sizeBytes;
        public string hashCode;
    }

    [MenuItem("HybridCLR/Prepare Local One DLL Build", priority = 300)]
    public static void GenerateAndCopy()
    {
        EnsureBootstrapScene();
        PrebuildCommand.GenerateAll();

        string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
        CompileAndCopyHotDll(projectDirectory);
        CopyAotMetadata(projectDirectory);
        BuildHotSceneBundle(projectDirectory);
        BuildAndCopyGameFrameworkResources(projectDirectory, true);
        AssetDatabase.Refresh();
        Debug.Log("Prepared fresh hot-update DLL, AOT metadata, and GameFramework resources.");
    }

    private static void PrepareHotContentOnly()
    {
        EnsureBootstrapScene();
        string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
        CompileAndCopyHotDll(projectDirectory);
        BuildHotSceneBundle(projectDirectory);
        BuildAndCopyGameFrameworkResources(projectDirectory, false);
        AssetDatabase.Refresh();
    }

    private static void CompileAndCopyHotDll(string projectDirectory)
    {
        string freshDirectory = Path.Combine(projectDirectory, "HybridCLRData", "FreshHotUpdateDlls",
            Guid.NewGuid().ToString("N"));
        string source = Path.Combine(freshDirectory, HotAssemblyName);
        try
        {
            CompileDllCommand.CompileDll(freshDirectory, EditorUserBuildSettings.activeBuildTarget,
                EditorUserBuildSettings.development);
            if (!File.Exists(source))
            {
                throw new BuildFailedException($"HybridCLR did not produce a fresh {HotAssemblyName}: {source}");
            }

            string destination = Path.Combine(projectDirectory, HotAssetDirectory, HotAssemblyName + ".bytes");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
            Debug.Log($"Freshly compiled {HotAssemblyName}: {source} -> {destination}");
        }
        finally
        {
            if (Directory.Exists(freshDirectory))
            {
                Directory.Delete(freshDirectory, true);
            }
        }
    }

    private static void BuildHotSceneBundle(string projectDirectory)
    {
        string outputDirectory = Path.Combine(projectDirectory, "Builds", "SceneBundleIntermediate");
        Directory.CreateDirectory(outputDirectory);
        AssetBundleBuild sceneBuild = new AssetBundleBuild
        {
            assetBundleName = "qipan_scene",
            assetNames = new[] { GameScenePath }
        };
        if (BuildPipeline.BuildAssetBundles(outputDirectory, new[] { sceneBuild },
                BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget) == null)
            throw new BuildFailedException("Cannot build the hot-update qipan scene bundle.");

        string source = Path.Combine(outputDirectory, sceneBuild.assetBundleName);
        string destination = Path.Combine(projectDirectory, HotAssetDirectory, "qipan-scene.bytes");
        File.Copy(source, destination, true);
        AssetDatabase.ImportAsset(HotAssetDirectory + "/qipan-scene.bytes", ImportAssetOptions.ForceUpdate);
        Debug.Log($"Built hot-update qipan scene bundle: {destination}");
    }

    [MenuItem("HybridCLR/Build Local One DLL Player", priority = 301)]
    public static void BuildLocalPlayer()
    {
        BuildLocalPlayerToDirectory(null);
    }

    public static void BuildLocalPlayerToDirectory(string playerOutputDirectory)
    {
        if (PlayerSettings.GetScriptingBackend(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget))
            != ScriptingImplementation.IL2CPP)
        {
            throw new BuildFailedException("HybridCLR Player requires IL2CPP scripting backend.");
        }

        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, AotCompatibilityFile),
            Guid.NewGuid().ToString("N"));
        AssetDatabase.Refresh();
        GenerateAndCopy();

        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
        string outputDirectory = string.IsNullOrWhiteSpace(playerOutputDirectory)
            ? Path.Combine(projectDirectory, "Builds", target.ToString())
            : Path.GetFullPath(playerOutputDirectory);
        Directory.CreateDirectory(outputDirectory);
        string extension = target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64 ? ".exe"
            : target == BuildTarget.Android ? ".apk"
            : target == BuildTarget.StandaloneOSX ? ".app" : string.Empty;
        string outputPath = Path.Combine(outputDirectory, PlayerSettings.productName + extension);
        List<string> enabledScenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                enabledScenes.Add(scene.path);
            }
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = enabledScenes.ToArray(),
            locationPathName = outputPath,
            target = target,
            options = EditorUserBuildSettings.development ? BuildOptions.Development : BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"HybridCLR Player build failed: {report.summary.result}");
        }

        Debug.Log($"Built HybridCLR Player: {outputPath}");
    }

    [MenuItem("HybridCLR/Publish Local Hot Update Files", priority = 302)]
    public static void PublishLocalHotUpdateFiles()
    {
        PublishLocalHotUpdateFilesIfChanged();
    }

    [MenuItem("HybridCLR/Export Readable Hot Update Manifest", priority = 303)]
    public static void ExportReadableHotUpdateManifest()
    {
        string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.Combine(projectDirectory, "Builds", "HotUpdateServer",
            EditorUserBuildSettings.activeBuildTarget.ToString());
        WriteReadableManifest(output);
        Debug.Log($"Exported readable hot-update manifest: {Path.Combine(output, "manifest.json")}");
    }

    public static bool PublishLocalHotUpdateFilesIfChanged()
    {
        PrepareHotContentOnly();
        if (string.IsNullOrEmpty(s_LastPackageDirectory))
        {
            throw new BuildFailedException("GameFramework resources were not built for publication.");
        }

        string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.Combine(projectDirectory, "Builds", "HotUpdateServer",
            EditorUserBuildSettings.activeBuildTarget.ToString());
        if (!HasPublishableChanges(output, s_LastPackageDirectory))
        {
            string readableManifest = Path.Combine(output, "manifest.json");
            if (!File.Exists(readableManifest))
            {
                WriteReadableManifest(output);
                Debug.Log("Published readable manifest.json for the existing hot-update version.");
                return true;
            }
            Debug.Log("Published no hot-update changes; kept existing version.json and skipped upload.");
            return false;
        }

        string publishRoot = Path.GetFullPath(Path.Combine(projectDirectory, "Builds", "HotUpdateServer"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(output).StartsWith(publishRoot, StringComparison.OrdinalIgnoreCase))
            throw new BuildFailedException("Hot-update output path is outside the publish directory.");
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }
        Directory.CreateDirectory(output);

        int fileCount = 0;
        foreach (string source in Directory.GetFiles(s_LastPackageDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = source.Substring(s_LastPackageDirectory.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
            string destination = Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
            fileCount++;
        }
        WriteReadableManifest(output);
        Debug.Log($"Published {fileCount} GameFramework updatable files to {output}. Serve this directory via HTTP.");
        return true;
    }

    private static void WriteReadableManifest(string directory)
    {
        string versionPath = Path.Combine(directory, "version.json");
        PublishVersionInfo version = JsonUtility.FromJson<PublishVersionInfo>(File.ReadAllText(versionPath));
        string[] lists = Directory.GetFiles(directory, "GameFrameworkVersion.*.dat");
        if (lists.Length != 1) throw new BuildFailedException("Expected one GameFramework version list for readable manifest.");

        byte[] decompressed = GameFramework.Utility.Compression.Decompress(File.ReadAllBytes(lists[0]));
        if (decompressed == null) throw new BuildFailedException("Cannot decompress GameFramework version list.");
        UpdatableVersionListSerializer serializer = new UpdatableVersionListSerializer();
        serializer.RegisterDeserializeCallback(0, BuiltinVersionListSerializer.UpdatableVersionListDeserializeCallback_V0);
        serializer.RegisterDeserializeCallback(1, BuiltinVersionListSerializer.UpdatableVersionListDeserializeCallback_V1);
        serializer.RegisterDeserializeCallback(2, BuiltinVersionListSerializer.UpdatableVersionListDeserializeCallback_V2);
        UpdatableVersionList list;
        using (MemoryStream stream = new MemoryStream(decompressed)) list = serializer.Deserialize(stream);
        if (!list.IsValid || list.InternalResourceVersion != version.internalResourceVersion)
            throw new BuildFailedException("GameFramework version list does not match version.json.");

        UpdatableVersionList.Resource[] resources = list.GetResources();
        ManifestResource[] entries = new ManifestResource[resources.Length];
        for (int i = 0; i < resources.Length; i++)
        {
            UpdatableVersionList.Resource resource = resources[i];
            string fileName = string.IsNullOrEmpty(resource.Variant)
                ? $"{resource.Name}.{resource.HashCode:x8}.{resource.Extension}"
                : $"{resource.Name}.{resource.Variant}.{resource.HashCode:x8}.{resource.Extension}";
            string filePath = Path.Combine(directory, fileName.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath)) throw new BuildFailedException($"Manifest resource is missing: {filePath}");
            entries[i] = new ManifestResource
            {
                resourceName = resource.Name,
                fileName = fileName.Replace('\\', '/'),
                sizeBytes = checked((int)new FileInfo(filePath).Length),
                hashCode = $"{resource.HashCode:x8}"
            };
        }
        Array.Sort(entries, (a, b) => StringComparer.Ordinal.Compare(a.fileName, b.fileName));
        ReadableManifest manifest = new ReadableManifest
        {
            internalResourceVersion = list.InternalResourceVersion,
            appVersion = version.appVersion,
            resources = entries
        };
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(manifest, true));
    }

    private static bool HasPublishableChanges(string previousDirectory, string nextDirectory)
    {
        string previousVersionPath = Path.Combine(previousDirectory, "version.json");
        string nextVersionPath = Path.Combine(nextDirectory, "version.json");
        if (!File.Exists(previousVersionPath) || !File.Exists(nextVersionPath)) return true;

        PublishVersionInfo previous;
        PublishVersionInfo next;
        try
        {
            previous = JsonUtility.FromJson<PublishVersionInfo>(File.ReadAllText(previousVersionPath));
            next = JsonUtility.FromJson<PublishVersionInfo>(File.ReadAllText(nextVersionPath));
        }
        catch (Exception)
        {
            return true;
        }
        if (previous == null || next == null || previous.schemaVersion != next.schemaVersion ||
            previous.appVersion != next.appVersion || previous.aotCompatibility != next.aotCompatibility)
            return true;

        Dictionary<string, string> oldFiles = ResourceHashes(previousDirectory);
        Dictionary<string, string> newFiles = ResourceHashes(nextDirectory);
        if (oldFiles.Count == 0 || oldFiles.Count != newFiles.Count) return true;
        foreach (KeyValuePair<string, string> file in oldFiles)
        {
            if (!newFiles.TryGetValue(file.Key, out string hash) || hash != file.Value) return true;
        }
        return false;
    }

    private static Dictionary<string, string> ResourceHashes(string directory)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (SHA256 sha = SHA256.Create())
        {
            foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(path);
                if (name.Equals("version.json", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ||
                    (name.StartsWith("GameFrameworkVersion.", StringComparison.OrdinalIgnoreCase) &&
                     name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))) continue;

                string relative = path.Substring(directory.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                using (FileStream stream = File.OpenRead(path))
                    result.Add(relative, Convert.ToBase64String(sha.ComputeHash(stream)));
            }
        }
        return result;
    }

    private static void CopyAotMetadata(string projectDirectory)
    {
        string generatedReference = Path.Combine(Application.dataPath, "HybridCLRGenerate", "AOTGenericReferences.cs");
        if (!File.Exists(generatedReference))
        {
            throw new BuildFailedException($"Missing generated AOT references: {generatedReference}");
        }

        string text = File.ReadAllText(generatedReference);
        int start = text.IndexOf("// {{ AOT assemblies", StringComparison.Ordinal);
        int end = start < 0 ? -1 : text.IndexOf("// }}", start, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            throw new BuildFailedException("Cannot read PatchedAOTAssemblyList from AOTGenericReferences.cs.");
        }

        string sourceDirectory = Path.Combine(projectDirectory, "HybridCLRData", "AssembliesPostIl2CppStrip",
            EditorUserBuildSettings.activeBuildTarget.ToString());
        string destinationDirectory = Path.Combine(Application.streamingAssetsPath, "aot-metadata");
        Directory.CreateDirectory(destinationDirectory);
        List<string> names = new List<string>();
        foreach (Match match in Regex.Matches(text.Substring(start, end - start), "\"([^\"]+\\.dll)\""))
        {
            string name = match.Groups[1].Value;
            string source = Path.Combine(sourceDirectory, name);
            if (!File.Exists(source))
            {
                throw new BuildFailedException($"Missing stripped AOT metadata assembly: {source}");
            }

            File.Copy(source, Path.Combine(destinationDirectory, name + ".bytes"), true);
            names.Add(name + ".bytes");
        }

        File.WriteAllLines(Path.Combine(Application.streamingAssetsPath, "aot-metadata.txt"), names);
        Debug.Log($"Copied {names.Count} stripped AOT metadata assemblies.");
    }

    private static void BuildAndCopyGameFrameworkResources(string projectDirectory, bool copyToStreamingAssets)
    {
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        Platform platform;
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
                platform = Platform.Windows;
                break;
            case BuildTarget.StandaloneWindows64:
                platform = Platform.Windows64;
                break;
            case BuildTarget.Android:
                platform = Platform.Android;
                break;
            case BuildTarget.iOS:
                platform = Platform.IOS;
                break;
            case BuildTarget.StandaloneOSX:
                platform = Platform.MacOS;
                break;
            case BuildTarget.StandaloneLinux64:
                platform = Platform.Linux;
                break;
            case BuildTarget.WebGL:
                platform = Platform.WebGL;
                break;
            default:
                throw new BuildFailedException($"GameFramework resource build does not support {target}.");
        }

        ResourceBuilderController builder = new ResourceBuilderController();
        if (!builder.Load())
        {
            throw new BuildFailedException("Cannot load GameFramework ResourceBuilder.xml.");
        }

        Platform configuredPlatforms = builder.Platforms;
        bool configuredPackage = builder.OutputPackageSelected;
        bool configuredFull = builder.OutputFullSelected;
        bool configuredPacked = builder.OutputPackedSelected;
        string configuredHandler = builder.BuildEventHandlerTypeName;
        builder.OutputDirectory = Path.Combine(projectDirectory, "BuildAssetBundle");
        Directory.CreateDirectory(builder.OutputDirectory);
        builder.Platforms = platform;
        builder.OutputPackageSelected = false;
        builder.OutputFullSelected = true;
        builder.OutputPackedSelected = copyToStreamingAssets;
        builder.BuildEventHandlerTypeName = typeof(HotUpdateBuildEventHandler).FullName;
        while (Directory.Exists(builder.OutputPackagePath) || Directory.Exists(builder.OutputFullPath)
            || Directory.Exists(builder.OutputPackedPath) || Directory.Exists(builder.BuildReportPath))
        {
            builder.InternalResourceVersion++;
        }

        if (!builder.RefreshCompressionHelper() || !builder.RefreshBuildEventHandler())
        {
            throw new BuildFailedException("Cannot initialize GameFramework resource build helpers.");
        }

        if (!builder.BuildResources())
        {
            throw new BuildFailedException($"GameFramework resource build failed. See {builder.BuildReportPath}.");
        }

        string fullDirectory = Path.Combine(builder.OutputFullPath, platform.ToString());
        string versionInfo = Path.Combine(fullDirectory, "version.json");
        if (!File.Exists(versionInfo))
        {
            throw new BuildFailedException($"GameFramework updatable version info is missing: {versionInfo}");
        }

        string[] fullFiles = Directory.GetFiles(fullDirectory, "*", SearchOption.AllDirectories);
        if (fullFiles.Length < 2)
        {
            throw new BuildFailedException($"GameFramework full build has no resource files: {fullDirectory}");
        }

        if (copyToStreamingAssets)
        {
            string packedDirectory = Path.Combine(builder.OutputPackedPath, platform.ToString());
            string packedList = Path.Combine(packedDirectory, "GameFrameworkList.dat");
            if (!File.Exists(packedList))
                throw new BuildFailedException($"GameFramework packed version list is missing: {packedList}");
            foreach (string source in Directory.GetFiles(packedDirectory, "*", SearchOption.AllDirectories))
            {
                string relative = source.Substring(packedDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destination = Path.Combine(Application.streamingAssetsPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, true);
                if (new FileInfo(source).Length != new FileInfo(destination).Length)
                {
                    throw new BuildFailedException($"GameFramework resource copy is incomplete: {relative}");
                }
            }
            string[] remoteLists = Directory.GetFiles(fullDirectory, "GameFrameworkVersion.*.dat");
            if (remoteLists.Length != 1)
                throw new BuildFailedException("Expected one full GameFramework version list.");
            File.Copy(remoteLists[0], Path.Combine(Application.streamingAssetsPath, "GameFrameworkVersion.dat"), true);
            File.Copy(versionInfo, Path.Combine(Application.streamingAssetsPath, "version.json"), true);
        }

        builder.Platforms = configuredPlatforms;
        builder.OutputPackageSelected = configuredPackage;
        builder.OutputFullSelected = configuredFull;
        builder.OutputPackedSelected = configuredPacked;
        builder.BuildEventHandlerTypeName = configuredHandler;
        if (!builder.Save())
        {
            throw new BuildFailedException("GameFramework resources were built, but ResourceBuilder.xml could not be saved.");
        }

        s_LastPackageDirectory = fullDirectory;
        Debug.Log($"Built {fullFiles.Length} GameFramework updatable files in {fullDirectory} (version {builder.InternalResourceVersion}, packed in Player: {copyToStreamingAssets}).");
    }

    private static void ValidateGameFrameworkPackage(string packageDirectory, string versionListPath, int expectedVersion)
    {
        PackageVersionListSerializer serializer = new PackageVersionListSerializer();
        serializer.RegisterDeserializeCallback(0, BuiltinVersionListSerializer.PackageVersionListDeserializeCallback_V0);
        serializer.RegisterDeserializeCallback(1, BuiltinVersionListSerializer.PackageVersionListDeserializeCallback_V1);
        serializer.RegisterDeserializeCallback(2, BuiltinVersionListSerializer.PackageVersionListDeserializeCallback_V2);

        PackageVersionList versionList;
        using (FileStream stream = File.OpenRead(versionListPath))
        {
            versionList = serializer.Deserialize(stream);
        }

        if (!versionList.IsValid || versionList.InternalResourceVersion != expectedVersion)
        {
            throw new BuildFailedException($"GameFramework version list is invalid or stale: {versionListPath}");
        }

        PackageVersionList.Resource[] resources = versionList.GetResources();
        HashSet<int> storedInFileSystem = new HashSet<int>();
        foreach (PackageVersionList.FileSystem fileSystem in versionList.GetFileSystems())
        {
            string fileSystemPath = Path.Combine(packageDirectory, fileSystem.Name + ".dat");
            if (!File.Exists(fileSystemPath))
            {
                throw new BuildFailedException($"GameFramework file system is missing: {fileSystemPath}");
            }

            foreach (int resourceIndex in fileSystem.GetResourceIndexes())
            {
                storedInFileSystem.Add(resourceIndex);
            }
        }

        for (int i = 0; i < resources.Length; i++)
        {
            if (storedInFileSystem.Contains(i))
            {
                continue;
            }

            PackageVersionList.Resource resource = resources[i];
            string name = string.IsNullOrEmpty(resource.Variant) ? resource.Name : resource.Name + "." + resource.Variant;
            string resourcePath = Path.Combine(packageDirectory, name + "." + resource.Extension);
            if (!File.Exists(resourcePath) || new FileInfo(resourcePath).Length != resource.Length)
            {
                throw new BuildFailedException($"GameFramework version list references a missing or incomplete resource: {resourcePath}");
            }
        }
    }

    private static void EnsureBootstrapScene()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "GameMain/Scenes/bootstrap.unity")))
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject bootstrap = new GameObject("HotUpdateBootstrap");
            bootstrap.AddComponent<BootstrapLoader>();
            SceneManager.MoveGameObjectToScene(bootstrap, scene);
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(BootstrapScenePath, true)
        };
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path != BootstrapScenePath)
            {
                scenes.Add(new EditorBuildSettingsScene(scene.path, false));
            }
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
