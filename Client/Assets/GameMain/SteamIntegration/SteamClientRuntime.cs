#if UNITY_STANDALONE_WIN && (!UNITY_EDITOR || UNITY_EDITOR_WIN)
using System;
using Steamworks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Zizouqi.SteamIntegration
{
    [Preserve]
    public sealed class SteamClientRuntime : MonoBehaviour
    {
        private static SteamClientRuntime instance;
        private bool initialized;
        private string displayToken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            var root = new GameObject(nameof(SteamClientRuntime));
            DontDestroyOnLoad(root);
            instance = root.AddComponent<SteamClientRuntime>();
        }

        private void Awake()
        {
            try
            {
                initialized = SteamAPI.Init();
                if (initialized)
                    Debug.Log($"Steamworks initialized (App ID {SteamUtils.GetAppID()}).");
                else
                    Debug.Log("Steamworks unavailable. Local play remains available.");
            }
            catch (Exception error) when (error is DllNotFoundException
                || error is EntryPointNotFoundException || error is BadImageFormatException)
            {
                Debug.LogWarning($"Could not load Steamworks: {error.Message}");
            }
        }

        [Preserve]
        public static void ShowMainMenu() => SetDisplayToken("#Status_MainMenu");

        [Preserve]
        public static void ShowSinglePlayer() => SetDisplayToken("#Status_SinglePlayer");

        private static void SetDisplayToken(string token)
        {
            if (instance == null || !instance.initialized || instance.displayToken == token) return;
            if (SteamFriends.SetRichPresence("steam_display", token))
                instance.displayToken = token;
        }

        private void Update()
        {
            if (initialized) SteamAPI.RunCallbacks();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (!initialized) return;
            SteamFriends.ClearRichPresence();
            SteamAPI.Shutdown();
            initialized = false;
        }
    }
}
#endif
