using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WorldTimelineDebug;
using Object = UnityEngine.Object;

public static class WorldTimelineCaveAuthoring
{
    private const string Folder = "Assets/GameMain/Art/WorldTimeline/BlackfangCave";
    private const string Region = "Assets/GameMain/Art/WorldTimeline/BirthRegion/Regions/Region_0_m9.prefab";
    private const string ScenePath = "Assets/GameMain/Scenes/terrain.unity";

    [MenuItem("Tools/World Timeline/Install Blender Cave Landmark %&F7")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play。");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var texture = (TextureImporter)AssetImporter.GetAtPath(Folder + "/CavePalette.png");
        texture.mipmapEnabled = false; texture.filterMode = FilterMode.Point;
        texture.textureCompression = TextureImporterCompression.Uncompressed; texture.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/CavePalette.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, Folder + "/CavePalette.mat");
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/CavePalette.png"));
        material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .05f);
        material.SetFloat("_Metallic", 0); material.enableInstancing = true; EditorUtility.SetDirty(material);
        var meshes = new Mesh[3];
        var models = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            string path = Folder + "/BlackfangCave_LOD" + i + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false; importer.isReadable = false;
            importer.importCameras = importer.importLights = false;
            importer.SaveAndReimport();
            meshes[i] = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();
            models[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Debug.Log($"Blackfang Cave LOD{i}: {meshes[i].triangles.Length / 3} triangles, {meshes[i].subMeshCount} submeshes");
        }
        // Standalone reusable prefab uses Unity LODGroup. The map region uses its own region tiers.
        var standalone = new GameObject("黑牙巢_洞窟地标");
        try
        {
            var lods = new LOD[3];
            for (int i = 0; i < 3; i++)
            {
                var model = Model(standalone.transform, "LOD" + i, models[i], material, Vector3.zero);
                lods[i] = new LOD(new[] { .24f, .10f, .025f }[i], new Renderer[] { model });
            }
            var group = standalone.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(standalone, Folder + "/BlackfangCave.prefab");
        }
        finally { Object.DestroyImmediate(standalone); }

        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var regionInScene = Object.FindObjectsByType<WorldTimelineRegion>(FindObjectsSortMode.None)
            .First(r => r.Node == new Vector2Int(0, -9));
        var site = regionInScene.transform.position;
        var cavePosition = new Vector3(0, 0, 8);
        var ground = Terrain.activeTerrains.FirstOrDefault(t =>
            site.x >= t.transform.position.x && site.x <= t.transform.position.x + t.terrainData.size.x
            && site.z + 8 >= t.transform.position.z && site.z + 8 <= t.transform.position.z + t.terrainData.size.z);
        if (ground != null) cavePosition.y = ground.SampleHeight(site + cavePosition) + ground.transform.position.y - site.y + .2f;
        if (!File.Exists(Folder + "/CaveUnityBefore.png")) Preview(scene, site, "Before");

        var contents = PrefabUtility.LoadPrefabContents(Region);
        try
        {
            var middle = contents.transform.Cast<Transform>().Single(t => t.name.StartsWith("Middle_"));
            var landmark = middle.Cast<Transform>().First(t => t.name.StartsWith("黑牙巢_"));
            var near = contents.transform.Cast<Transform>().Single(t => t.name.StartsWith("Near_"));
            foreach (var prop in middle.Cast<Transform>().Concat(near.Cast<Transform>()).ToArray())
            {
                if (prop == landmark) continue;
                var delta = contents.transform.InverseTransformPoint(prop.position) - cavePosition;
                if (Mathf.Abs(delta.x) < 6.5f && Mathf.Abs(delta.z) < 6)
                    Object.DestroyImmediate(prop.gameObject);
            }
            foreach (var child in landmark.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            landmark.localPosition = Vector3.zero; landmark.localRotation = Quaternion.identity;
            landmark.localScale = Vector3.one;
            // Keep the node and chess piece clear in front of the entrance.
            var nearModel = Model(landmark, "洞窟_Blender", models[0], material, cavePosition);
            Debug.Log("Cave bounds " + nearModel.bounds);
            var far = contents.transform.Cast<Transform>().Single(t => t.name.StartsWith("Far_"));
            foreach (var child in far.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var silhouette = Model(far, "洞窟_远景", models[2], material, cavePosition);
            silhouette.shadowCastingMode = ShadowCastingMode.Off;
            contents.GetComponent<WorldTimelineRegion>().RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(contents, Region);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        AssetDatabase.SaveAssets();
        Preview(scene, site, "After");
        Debug.Log("Blender 洞窟已替换黑牙巢旧地标，保留地形、节点坐标、相机和游戏数据。预览在 " + Folder);
    }

    private static MeshRenderer Model(Transform parent, string name, GameObject model, Material material, Vector3 position)
    {
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(model);
        obj.name = name; obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
        obj.transform.localRotation = Quaternion.Euler(0, 180, 0) * obj.transform.localRotation;
        var renderer = obj.GetComponentsInChildren<MeshRenderer>(true).Single(); renderer.sharedMaterial = material;
        return renderer;
    }

    private static void Preview(Scene scene, Vector3 site, string suffix)
    {
        var obj = new GameObject("Temporary cave preview"); SceneManager.MoveGameObjectToScene(obj, scene);
        var camera = obj.AddComponent<Camera>(); camera.orthographic = false; camera.fieldOfView = 48;
        camera.transform.rotation = Quaternion.Euler(48, -20, 0);
        camera.transform.position = site + new Vector3(0, 1, 5) - camera.transform.forward * 38;
        camera.nearClipPlane = .3f; camera.farClipPlane = 300;
        var rt = new RenderTexture(1400, 1000, 24); rt.Create(); var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); image.Apply();
            File.WriteAllBytes(Folder + "/CaveUnity" + suffix + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(obj);
        }
    }
}
