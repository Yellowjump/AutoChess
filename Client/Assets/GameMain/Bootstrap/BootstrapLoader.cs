using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameFramework;
using GameFramework.Fsm;
using GameFramework.Procedure;
using GameFramework.Resource;
using HybridCLR;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityGameFramework.Runtime;

namespace Zizouqi.Bootstrap
{
    // The first scene contains only AOT code. Its temporary GameFramework modules are
    // released before the hot scene creates its own GameFramework components.
    public sealed class BootstrapLoader : MonoBehaviour
    {
        private const string ServerUrlFile = "hot-update-url.txt";
        private const string CompatibilityFile = "aot-compatibility.txt";
        private const string VersionFile = "version.json";
        private const string VersionListFile = "GameFrameworkVersion.dat";
        private const string AssemblyAsset = "Assets/GameMain/HotUpdateAssets/Assembly-CSharp.dll.bytes";
        private const string SceneAsset = "Assets/GameMain/HotUpdateAssets/qipan-scene.bytes";

        [Serializable]
        private sealed class VersionInfo
        {
            public int schemaVersion;
            public string appVersion;
            public string aotCompatibility;
            public int internalResourceVersion;
            public int versionListLength;
            public int versionListHashCode;
            public int versionListCompressedLength;
            public int versionListCompressedHashCode;
        }

        internal static BootstrapLoader Instance { get; private set; }

        private GameObject m_FrameworkRoot;
        private ResourceComponent m_Resource;
        private string m_AotCompatibility;
        private string m_ServerUrl;
        private VersionInfo m_BuiltInVersion;
        private VersionInfo m_SelectedVersion;
        private bool m_UsingRemoteVersion;
        private readonly Dictionary<string, long> m_ActiveDownloads = new Dictionary<string, long>();
        private IResourceManager m_ProgressResourceManager;
        private long m_TotalDownloadBytes;
        private long m_CompletedDownloadBytes;
        private bool m_ShowDownloadProgress;

        private void OnGUI()
        {
            if (!m_ShowDownloadProgress || m_TotalDownloadBytes <= 0) return;

            long downloaded = m_CompletedDownloadBytes;
            foreach (long current in m_ActiveDownloads.Values) downloaded += current;
            downloaded = Math.Min(downloaded, m_TotalDownloadBytes);
            float progress = (float)downloaded / m_TotalDownloadBytes;
            float width = Mathf.Min(520f, Screen.width - 40f);
            float left = (Screen.width - width) * 0.5f;
            float top = Screen.height * 0.82f;
            Rect bar = new Rect(left, top, width, 18f);
            Color previousColor = GUI.color;
            GUI.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(0.24f, 0.72f, 0.46f, 1f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * progress, bar.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUIStyle label = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height / 54f))
            };
            GUI.Label(new Rect(left, top + 24f, width, 28f),
                $"{progress:P0}   {FormatSize(downloaded)} / {FormatSize(m_TotalDownloadBytes)}", label);
        }

        private static string FormatSize(long bytes)
        {
            return bytes >= 1024 * 1024
                ? $"{bytes / (1024f * 1024f):0.0} MB"
                : $"{bytes / 1024f:0.0} KB";
        }

        private IEnumerator Start()
        {
#if UNITY_EDITOR
            // In the Editor, business scripts are already compiled and the scene can be
            // loaded directly without reading the Player's HybridCLR resource package.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/GameMain/Scenes/qipan.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield break;
#else
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateBootstrapCamera();
            CreateFrameworkRoot();
            // Let the components complete Start before using ResourceComponent.
            yield return null;
            m_Resource = GameEntry.GetComponent<ResourceComponent>();
            if (m_Resource == null)
            {
                Debug.LogError("Bootstrap ResourceComponent was not initialized.");
                yield break;
            }

            IProcedureManager procedures = GameFrameworkEntry.GetModule<IProcedureManager>();
            procedures.Initialize(GameFrameworkEntry.GetModule<IFsmManager>(),
                new ProcedureBootstrapInit(), new ProcedureCheckVersion(),
                new ProcedureUpdateResources(), new ProcedureLoadHotUpdate());
            procedures.StartProcedure<ProcedureBootstrapInit>();
#endif
        }

        private void CreateBootstrapCamera()
        {
            GameObject cameraObject = new GameObject("BootstrapCamera");
            cameraObject.transform.SetParent(transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.10f, 0.14f, 1f);
            camera.cullingMask = 0;
            camera.depth = -100f;
        }

        private void CreateFrameworkRoot()
        {
            m_FrameworkRoot = new GameObject("BootstrapGameFramework");
            m_FrameworkRoot.SetActive(false);
            BaseComponent baseComponent = m_FrameworkRoot.AddComponent<BaseComponent>();
            baseComponent.EditorResourceMode = false;
            m_FrameworkRoot.AddComponent<EventComponent>();
            m_FrameworkRoot.AddComponent<DownloadComponent>();
            m_FrameworkRoot.AddComponent<ObjectPoolComponent>();
            m_FrameworkRoot.AddComponent<FileSystemComponent>();
            ResourceComponent resource = m_FrameworkRoot.AddComponent<ResourceComponent>();
            typeof(ResourceComponent).GetField("m_ResourceMode", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(resource, ResourceMode.Updatable);
            DontDestroyOnLoad(m_FrameworkRoot);
            m_FrameworkRoot.SetActive(true);
        }

        private void OnDestroy()
        {
            DetachProgressEvents();
            if (Instance == this) Instance = null;
            if (m_FrameworkRoot != null)
            {
                GameEntry.Shutdown(ShutdownType.None);
                Destroy(m_FrameworkRoot);
            }
        }

        internal void BeginInitialization(Action<bool> completed) => StartCoroutine(Initialize(completed));
        internal void BeginVersionCheck(Action<bool> completed) => StartCoroutine(CheckVersion(completed));
        internal void BeginResourceUpdate(Action<bool> completed) => StartCoroutine(UpdateResources(completed));
        internal void BeginHotUpdateLoad() => StartCoroutine(LoadHotUpdate());

        private IEnumerator Initialize(Action<bool> completed)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(BuiltInUri(CompatibilityFile)))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("AOT compatibility ID load failed: " + request.error);
                    completed(false);
                    yield break;
                }
                m_AotCompatibility = request.downloadHandler.text.Trim();
            }

            using (UnityWebRequest request = UnityWebRequest.Get(BuiltInUri("aot-metadata.txt")))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("AOT metadata list load failed: " + request.error);
                    completed(false);
                    yield break;
                }
                string[] names = request.downloadHandler.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string name in names)
                {
                    using (UnityWebRequest metadata = UnityWebRequest.Get(BuiltInUri("aot-metadata/" + name)))
                    {
                        yield return metadata.SendWebRequest();
                        if (metadata.result != UnityWebRequest.Result.Success ||
                            RuntimeApi.LoadMetadataForAOTAssembly(metadata.downloadHandler.data,
                                HomologousImageMode.SuperSet) != LoadImageErrorCode.OK)
                        {
                            Debug.LogError("AOT metadata load failed: " + name);
                            completed(false);
                            yield break;
                        }
                    }
                }
            }

            using (UnityWebRequest request = UnityWebRequest.Get(BuiltInUri(VersionFile)))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Built-in version info load failed: " + request.error);
                    completed(false);
                    yield break;
                }
                m_BuiltInVersion = ParseVersion(request.downloadHandler.text);
            }
            completed(m_BuiltInVersion != null);
        }

        private IEnumerator CheckVersion(Action<bool> completed)
        {
            m_SelectedVersion = m_BuiltInVersion;
            m_ServerUrl = string.Empty;
            using (UnityWebRequest request = UnityWebRequest.Get(BuiltInUri(ServerUrlFile)))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                    m_ServerUrl = request.downloadHandler.text.Trim().TrimEnd('/');
            }
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
                if (arguments[i] == "-hotUpdateUrl") m_ServerUrl = arguments[i + 1].Trim().TrimEnd('/');

            if (!string.IsNullOrEmpty(m_ServerUrl))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(m_ServerUrl + "/" + VersionFile))
                {
                    request.timeout = 10;
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        VersionInfo remote = ParseVersion(request.downloadHandler.text);
                        if (remote != null)
                        {
                            m_SelectedVersion = remote;
                            m_UsingRemoteVersion = true;
                        }
                    }
                    else Debug.LogWarning("Version check failed: " + request.error);
                }
            }

            if (!m_UsingRemoteVersion)
            {
                string lastGood = Path.Combine(Application.persistentDataPath, "hot-update", "last-good-version.json");
                if (File.Exists(lastGood))
                {
                    VersionInfo cached = ParseVersion(File.ReadAllText(lastGood));
                    if (cached != null) m_SelectedVersion = cached;
                }
            }
            Debug.Log("Selected GameFramework resource version " + m_SelectedVersion.internalResourceVersion);
            completed(true);
        }

        private VersionInfo ParseVersion(string json)
        {
            try
            {
                VersionInfo info = JsonUtility.FromJson<VersionInfo>(json);
                return info != null && info.schemaVersion == 1 && info.appVersion == Application.version &&
                    info.aotCompatibility == m_AotCompatibility && info.internalResourceVersion >= 0 &&
                    info.versionListLength > 0 && info.versionListCompressedLength > 0 ? info : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Invalid version info: " + exception.Message);
                return null;
            }
        }

        private IEnumerator UpdateResources(Action<bool> completed)
        {
            string writableList = Path.Combine(Application.persistentDataPath, VersionListFile);
            bool needsVersionList = m_Resource.CheckVersionList(m_SelectedVersion.internalResourceVersion) ==
                CheckVersionListResult.NeedUpdate;
            if (needsVersionList && m_UsingRemoteVersion)
            {
                m_Resource.UpdatePrefixUri = m_ServerUrl;
                bool finished = false;
                bool succeeded = false;
                m_Resource.UpdateVersionList(m_SelectedVersion.versionListLength,
                    m_SelectedVersion.versionListHashCode, m_SelectedVersion.versionListCompressedLength,
                    m_SelectedVersion.versionListCompressedHashCode,
                    new UpdateVersionListCallbacks((path, uri) => { succeeded = true; finished = true; },
                        (uri, error) => { Debug.LogWarning("Version list update failed: " + error); finished = true; }));
                while (!finished) yield return null;
                if (!succeeded)
                {
                    completed(false);
                    yield break;
                }
            }
            else if (needsVersionList)
            {
                if (m_SelectedVersion.internalResourceVersion != m_BuiltInVersion.internalResourceVersion)
                {
                    m_SelectedVersion = m_BuiltInVersion;
                    Debug.LogWarning("Cached version list missing; using built-in resource version.");
                }
                using (UnityWebRequest request = UnityWebRequest.Get(BuiltInUri(VersionListFile)))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError("Built-in version list load failed: " + request.error);
                        completed(false);
                        yield break;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(writableList));
                    byte[] versionList = GameFramework.Utility.Compression.Decompress(request.downloadHandler.data);
                    if (versionList == null)
                    {
                        Debug.LogError("Built-in version list decompression failed.");
                        completed(false);
                        yield break;
                    }
                    File.WriteAllBytes(writableList, versionList);
                }
            }

            if (!string.IsNullOrEmpty(m_ServerUrl)) m_Resource.UpdatePrefixUri = m_ServerUrl;
            bool checkedResources = false;
            int missingCount = 0;
            m_Resource.CheckResources((moved, removed, update, length, zipped) =>
            {
                missingCount = update;
                m_TotalDownloadBytes = zipped;
                checkedResources = true;
            });
            while (!checkedResources) yield return null;
            if (missingCount > 0)
            {
                if (string.IsNullOrEmpty(m_ServerUrl))
                {
                    Debug.LogError("Resources are missing and no update server is configured.");
                    completed(false);
                    yield break;
                }
                bool finished = false;
                bool succeeded = false;
                Debug.Log("Downloading " + missingCount + " GameFramework resources.");
                m_CompletedDownloadBytes = 0;
                m_ActiveDownloads.Clear();
                m_ShowDownloadProgress = true;
                AttachProgressEvents();
                m_Resource.UpdateResources((group, result) => { succeeded = result; finished = true; });
                while (!finished) yield return null;
                DetachProgressEvents();
                m_ShowDownloadProgress = false;
                if (!succeeded)
                {
                    Debug.LogError("GameFramework resource download failed.");
                    completed(false);
                    yield break;
                }
            }

            if (m_UsingRemoteVersion)
            {
                string cache = Path.Combine(Application.persistentDataPath, "hot-update", "last-good-version.json");
                Directory.CreateDirectory(Path.GetDirectoryName(cache));
                File.WriteAllText(cache, JsonUtility.ToJson(m_SelectedVersion));
            }
            Debug.Log("GameFramework resources ready: " + m_SelectedVersion.internalResourceVersion);
            completed(true);
        }

        private void AttachProgressEvents()
        {
            m_ProgressResourceManager = GameFrameworkEntry.GetModule<IResourceManager>();
            m_ProgressResourceManager.ResourceUpdateStart += OnResourceUpdateStart;
            m_ProgressResourceManager.ResourceUpdateChanged += OnResourceUpdateChanged;
            m_ProgressResourceManager.ResourceUpdateSuccess += OnResourceUpdateSuccess;
        }

        private void DetachProgressEvents()
        {
            if (m_ProgressResourceManager == null) return;
            m_ProgressResourceManager.ResourceUpdateStart -= OnResourceUpdateStart;
            m_ProgressResourceManager.ResourceUpdateChanged -= OnResourceUpdateChanged;
            m_ProgressResourceManager.ResourceUpdateSuccess -= OnResourceUpdateSuccess;
            m_ProgressResourceManager = null;
        }

        private void OnResourceUpdateStart(object sender, GameFramework.Resource.ResourceUpdateStartEventArgs e)
        {
            m_ActiveDownloads[e.Name] = e.CurrentLength;
        }

        private void OnResourceUpdateChanged(object sender, GameFramework.Resource.ResourceUpdateChangedEventArgs e)
        {
            m_ActiveDownloads[e.Name] = e.CurrentLength;
        }

        private void OnResourceUpdateSuccess(object sender, GameFramework.Resource.ResourceUpdateSuccessEventArgs e)
        {
            m_ActiveDownloads.Remove(e.Name);
            m_CompletedDownloadBytes += e.CompressedLength;
        }

        private IEnumerator LoadHotUpdate()
        {
            byte[] assemblyBytes = null;
            yield return LoadBytesAsset(AssemblyAsset, bytes => assemblyBytes = bytes);
            if (assemblyBytes == null)
            {
                Debug.LogError("Hot-update DLL is missing from GameFramework resources.");
                yield break;
            }

            try
            {
                Assembly assembly = Assembly.Load(assemblyBytes);
                Debug.Log("Loaded " + assembly.GetName().Name + " from GameFramework resource (" +
                    assemblyBytes.Length + " bytes).");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                yield break;
            }

            byte[] sceneBytes = null;
            yield return LoadBytesAsset(SceneAsset, bytes => sceneBytes = bytes);
            if (sceneBytes == null)
            {
                Debug.LogError("Hot-update scene bundle is missing from GameFramework resources.");
                yield break;
            }

            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromMemoryAsync(sceneBytes);
            yield return bundleRequest;
            AssetBundle sceneBundle = bundleRequest.assetBundle;
            if (sceneBundle == null || sceneBundle.GetAllScenePaths().Length != 1)
            {
                Debug.LogError("Hot-update scene bundle is invalid.");
                yield break;
            }
            string scenePath = sceneBundle.GetAllScenePaths()[0];

            GameEntry.Shutdown(ShutdownType.None);
            Destroy(m_FrameworkRoot);
            m_FrameworkRoot = null;
            yield return null;
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
            sceneBundle.Unload(false);
            Destroy(gameObject);
        }

        private IEnumerator LoadBytesAsset(string name, Action<byte[]> completed)
        {
            bool finished = false;
            m_Resource.LoadAsset(name, typeof(TextAsset), new LoadAssetCallbacks(
                (assetName, asset, duration, userData) =>
                {
                    TextAsset text = asset as TextAsset;
                    completed(text == null ? null : text.bytes);
                    finished = true;
                },
                (assetName, status, error, userData) =>
                {
                    Debug.LogError("GameFramework asset load failed: " + name + ": " + error);
                    finished = true;
                }));
            while (!finished) yield return null;
        }

        private static string BuiltInUri(string name)
        {
            string path = Path.Combine(Application.streamingAssetsPath, name);
            return Path.IsPathRooted(path) ? new Uri(path).AbsoluteUri : path;
        }
    }
}
