using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityGameFramework.Editor.ResourceTools;

public sealed class HotUpdateBuildEventHandler : IBuildEventHandler
{
    [Serializable]
    private sealed class VersionInfo
    {
        public int schemaVersion = 1;
        public string appVersion;
        public string aotCompatibility;
        public int internalResourceVersion;
        public int versionListLength;
        public int versionListHashCode;
        public int versionListCompressedLength;
        public int versionListCompressedHashCode;
    }

    public bool ContinueOnFailure => false;

    public void OnPreprocessAllPlatforms(string productName, string companyName, string gameIdentifier,
        string gameFrameworkVersion, string unityVersion, string applicableGameVersion, int internalResourceVersion,
        Platform platforms, AssetBundleCompressionType assetBundleCompression, string compressionHelperTypeName,
        bool additionalCompressionSelected, bool forceRebuildAssetBundleSelected, string buildEventHandlerTypeName,
        string outputDirectory, BuildAssetBundleOptions buildAssetBundleOptions, string workingPath,
        bool outputPackageSelected, string outputPackagePath, bool outputFullSelected, string outputFullPath,
        bool outputPackedSelected, string outputPackedPath, string buildReportPath) { }

    public void OnPreprocessPlatform(Platform platform, string workingPath, bool outputPackageSelected,
        string outputPackagePath, bool outputFullSelected, string outputFullPath, bool outputPackedSelected,
        string outputPackedPath) { }

    public void OnBuildAssetBundlesComplete(Platform platform, string workingPath, bool outputPackageSelected,
        string outputPackagePath, bool outputFullSelected, string outputFullPath, bool outputPackedSelected,
        string outputPackedPath, AssetBundleManifest assetBundleManifest) { }

    public void OnOutputUpdatableVersionListData(Platform platform, string versionListPath,
        int versionListLength, int versionListHashCode, int versionListCompressedLength,
        int versionListCompressedHashCode)
    {
        string compatibilityPath = Path.Combine(Application.streamingAssetsPath, "aot-compatibility.txt");
        if (!File.Exists(compatibilityPath))
            throw new InvalidOperationException("Build the AOT Player before publishing an update.");

        string folder = Path.GetDirectoryName(versionListPath);
        string versionFolder = Directory.GetParent(folder).Name;
        string[] parts = versionFolder.Split('_');
        VersionInfo info = new VersionInfo
        {
            appVersion = Application.version,
            aotCompatibility = File.ReadAllText(compatibilityPath).Trim(),
            internalResourceVersion = int.Parse(parts[parts.Length - 1]),
            versionListLength = versionListLength,
            versionListHashCode = versionListHashCode,
            versionListCompressedLength = versionListCompressedLength,
            versionListCompressedHashCode = versionListCompressedHashCode
        };
        File.WriteAllText(Path.Combine(folder, "version.json"), JsonUtility.ToJson(info, true));
    }

    public void OnPostprocessPlatform(Platform platform, string workingPath, bool outputPackageSelected,
        string outputPackagePath, bool outputFullSelected, string outputFullPath, bool outputPackedSelected,
        string outputPackedPath, bool isSuccess) { }

    public void OnPostprocessAllPlatforms(string productName, string companyName, string gameIdentifier,
        string gameFrameworkVersion, string unityVersion, string applicableGameVersion, int internalResourceVersion,
        Platform platforms, AssetBundleCompressionType assetBundleCompression, string compressionHelperTypeName,
        bool additionalCompressionSelected, bool forceRebuildAssetBundleSelected, string buildEventHandlerTypeName,
        string outputDirectory, BuildAssetBundleOptions buildAssetBundleOptions, string workingPath,
        bool outputPackageSelected, string outputPackagePath, bool outputFullSelected, string outputFullPath,
        bool outputPackedSelected, string outputPackedPath, string buildReportPath) { }
}
