using UnityEditor;

[InitializeOnLoad]
internal static class BootstrapEditorScenes
{
    private const string TerrainScenePath = "Assets/GameMain/Scenes/terrain.unity";

    static BootstrapEditorScenes()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            SetTerrainEnabled(true);
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SetTerrainEnabled(false);
        }
    }

    private static void SetTerrainEnabled(bool enabled)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path != TerrainScenePath || scenes[i].enabled == enabled) continue;
            scenes[i] = new EditorBuildSettingsScene(scenes[i].path, enabled);
            EditorBuildSettings.scenes = scenes;
            return;
        }
    }
}
