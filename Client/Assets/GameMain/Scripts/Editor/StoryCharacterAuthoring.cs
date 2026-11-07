using System.IO;
using UnityEditor;
using UnityEngine;

public static class StoryCharacterAuthoring
{
    [MenuItem("Tools/World Timeline/Build Story Character Prefabs %#F9")]
    public static void Build()
    {
        const string root = "Assets/GameMain/Art/Characters";
        string output = root + "/Resources/StoryCharacters";
        Directory.CreateDirectory(output);
        AssetDatabase.Refresh();
        foreach (string identity in new[] { "ShenYan", "LuYing", "Chongshan", "Zhuxing" })
        {
            string source = root + "/" + identity;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(source + "/" + identity + "Palette.png");
            texture.filterMode = FilterMode.Point;
            var material = AssetDatabase.LoadAssetAtPath<Material>(source + "/" + identity + ".mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, source + "/" + identity + ".mat");
            }
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", .1f);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(source + "/" + identity + "_Parts.fbx");
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameMain/Prefeb/Charactor/qizi0/KyleRobot.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(rig);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = identity;
            var geometry = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                StoryCharacterSkinBuilder.Bind(instance, geometry, source, identity, material);
                PrefabUtility.SaveAsPrefabAsset(instance, output + "/" + identity + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(geometry);
                Object.DestroyImmediate(instance);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Story character humanoid prefabs built.");
    }
}
