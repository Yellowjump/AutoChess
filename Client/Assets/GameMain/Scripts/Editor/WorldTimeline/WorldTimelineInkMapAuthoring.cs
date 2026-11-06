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

public static class WorldTimelineInkMapAuthoring
{
    public const string Folder = "Assets/GameMain/Art/WorldTimeline/InkMap";
    public const string RootName = "轮回水墨地图_山河分区";
    private const string ScenePath = "Assets/GameMain/Scenes/terrain.unity";
    public const float RegionWidth = 8.8f, RegionDepth = 7.6f;
    public const float LandmarkWidth = 3.4f, LandmarkDepth = 3.1f;
    public readonly struct RegionSpec
    {
        public readonly string Name;
        public readonly int Q, R, Landmark;
        public readonly bool Mountain;
        public RegionSpec(string name, int q, int r, bool mountain, int landmark)
        { Name = name; Q = q; R = r; Mountain = mountain; Landmark = landmark; }
    }
    public static readonly RegionSpec[] Definitions = {
        new RegionSpec("Outer_青禾乡野", 0, -9, false, 0),
        new RegionSpec("Transition_灵山古隘", 0, -6, true, 2),
        new RegionSpec("Outer_临溪渡口", 4, -9, false, 1),
        new RegionSpec("Outer_柳岸农庄", 9, -8, false, 0),
        new RegionSpec("Outer_重山乡野", 9, -2, false, 0),
        new RegionSpec("Outer_听潮水乡", 7, 1, false, 1),
        new RegionSpec("Outer_照夜村落", 2, 7, false, 0),
        new RegionSpec("Outer_松溪古祠", -4, 9, false, 3),
        new RegionSpec("Outer_逐星渡", -9, 5, false, 1),
        new RegionSpec("Outer_西原乡野", -7, -1, false, 0),
        new RegionSpec("Transition_东岭山门", 5, -3, true, 2),
        new RegionSpec("Transition_云松古寺", 3, 3, true, 3),
        new RegionSpec("Transition_北麓古隘", -3, 5, true, 2),
        new RegionSpec("Transition_西山旧观", -5, 0, true, 3),
        new RegionSpec("Transition_东麓松境", 7, -5, true, 2),
        new RegionSpec("Transition_竹溪灵境", 2, 5, true, 3),
        new RegionSpec("Transition_北麓松境", -3, 7, true, 3),
        new RegionSpec("Transition_西岭松林", -7, 3, true, 2)
    };

    [MenuItem("Tools/World Timeline/Build Ink Map Sample")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请停止 Play 后构建水墨样板。");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        GameObject draft = null;
        try
        {
            var source = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<WorldTimelineMapLayout>(true))
                .OrderByDescending(entry => entry.FlatSurface).FirstOrDefault();
            // Existing layout values are retained; a new map also builds without any Terrain or collider.
            var mapOrigin = source != null ? source.Origin : Vector3.zero;
            float spacing = source != null && source.NodeSpacing > .01f ? source.NodeSpacing : 18;
            Directory.CreateDirectory(Folder + "/Regions");
            AssetDatabase.Refresh();
            int layer = EnsureLayer();
            var rendererData = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>("Assets/GameMain/RenderSetting/MainCameraRendererData.asset"));
            rendererData.FindProperty("m_TransparentLayerMask").intValue |= 1 << layer;
            rendererData.ApplyModifiedProperties();
            var shader = Shader.Find("WorldTimeline/InkScenery");
            if (shader == null) throw new InvalidOperationException("缺少水墨材质 Shader。");
            var mesh = MakeQuad();
            var paper = Material("Paper", shader, null);
            paper.SetColor("_BaseColor", new Color(.96f, .95f, .92f));
            paper.SetFloat("_Paper", 1); paper.renderQueue = 2900;
            draft = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(draft, scene);
            var layout = draft.AddComponent<WorldTimelineMapLayout>();
            layout.Origin = mapOrigin;
            layout.NodeSpacing = spacing;
            layout.FlatSurface = layout.InkPalette = true;
            layout.CameraPitch = 57; layout.CameraYaw = 180; layout.CameraFieldOfView = 42;
            draft.AddComponent<WorldTimelineInkMap>();
            Plane(draft.transform, "宣纸底面", mesh, paper, mapOrigin - Vector3.up * .1f,
                Vector3.one * spacing * 120, layer);
            PrepareTexture(Folder + "/LandmarkAtlas.png");
            var regions = Definitions.Select(spec => Region(draft.transform, spec, mesh, shader, layout, layer)).ToArray();
            draft.AddComponent<WorldTimelineRegionCulling>().Configure(regions, spacing * .6f);
            string path = Folder + "/InkMapSample.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(draft, path);
            Object.DestroyImmediate(draft); draft = null;
            foreach (var old in scene.GetRootGameObjects().Where(go => go.GetComponent<WorldTimelineInkMap>() != null).ToArray())
                Object.DestroyImmediate(old);
            var installed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            installed.name = RootName;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("独立平面地图已接入：" + regions.Length + " 个水墨区块与放大地标，无 Terrain 依赖。" + path);
        }
        finally
        {
            if (draft != null) Object.DestroyImmediate(draft);
            if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static WorldTimelineRegion Region(Transform root, RegionSpec spec,
        Mesh mesh, Shader shader, WorldTimelineMapLayout layout, int layer)
    {
        string name = spec.Name;
        var node = new Vector2Int(spec.Q, spec.R);
        string image = spec.Mountain ? "InnerPass.png" : "OuterCountryside.png";
        string imagePath = Folder + "/" + image;
        PrepareTexture(imagePath);
        var material = Material(name, shader, AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath));
        var region = new GameObject(name);
        region.layer = layer;
        region.transform.SetParent(root, false);
        region.transform.position = layout.Project(node.x, node.y);
        var near = new GameObject("Near_预留局部摆件"); near.transform.SetParent(region.transform, false);
        var middle = new GameObject("Middle_独立水墨底景"); middle.transform.SetParent(region.transform, false);
        var far = new GameObject("Far_区域轮廓"); far.transform.SetParent(region.transform, false);
        var scale = new Vector3(RegionWidth, 1, RegionDepth) * layout.NodeSpacing;
        Plane(middle.transform, "InkArtwork", mesh, material, region.transform.position + Vector3.up * .015f, scale, layer);
        Plane(far.transform, "InkArtwork", mesh, material, region.transform.position + Vector3.up * .015f, scale, layer);
        var landmark = Material("Landmark_" + spec.Landmark, shader,
            AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/LandmarkAtlas.png"));
        landmark.SetTextureScale("_BaseMap", new Vector2(.5f,.5f));
        landmark.SetTextureOffset("_BaseMap", new Vector2(spec.Landmark % 2 * .5f, spec.Landmark < 2 ? .5f : 0));
        landmark.renderQueue = 3010;
        var landmarkPosition = region.transform.position + new Vector3(-.55f, .05f / layout.NodeSpacing, .65f) * layout.NodeSpacing;
        var landmarkScale = new Vector3(LandmarkWidth, 1, LandmarkDepth) * layout.NodeSpacing;
        Plane(middle.transform, "Landmark_主地标", mesh, landmark, landmarkPosition, landmarkScale, layer);
        Plane(far.transform, "Landmark_远景地标", mesh, landmark, landmarkPosition, landmarkScale, layer);
        var component = region.AddComponent<WorldTimelineRegion>();
        component.Configure(node, new Bounds(Vector3.zero, new Vector3(scale.x, layout.NodeSpacing * .2f, scale.z)), near, middle, far);
        string path = Folder + "/Regions/" + name + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(region, path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        instance.transform.position = region.transform.position;
        Object.DestroyImmediate(region);
        return instance.GetComponent<WorldTimelineRegion>();
    }
    private static void PrepareTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("缺少水墨图块：" + path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
    private static void Plane(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, int layer)
    {
        var go = new GameObject(name); go.layer = layer;
        go.transform.SetParent(parent, false); go.transform.position = position; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }
    private static Mesh MakeQuad()
    {
        string path = Folder + "/InkQuad.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = "Ink ground quad" };
        mesh.vertices = new[] { new Vector3(.5f,0,.5f), new Vector3(-.5f,0,.5f), new Vector3(.5f,0,-.5f), new Vector3(-.5f,0,-.5f) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0,2,1,1,2,3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,path); return mesh;
    }
    private static Material Material(string name, Shader shader, Texture texture)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
        material.SetTexture("_BaseMap", texture != null ? texture : Texture2D.whiteTexture);
        material.SetColor("_BaseColor", Color.white); material.SetFloat("_Paper",0);
        material.renderQueue = 3000; EditorUtility.SetDirty(material); return material;
    }
    private static int EnsureLayer()
    {
        var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = manager.FindProperty("layers");
        for (int i=8;i<32;i++) if (layers.GetArrayElementAtIndex(i).stringValue == WorldTimelineInkMap.LayerName) return i;
        for (int i=31;i>=13;i--) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
        { layers.GetArrayElementAtIndex(i).stringValue = WorldTimelineInkMap.LayerName; manager.ApplyModifiedProperties(); return i; }
        throw new InvalidOperationException("没有可用图层用于隔离水墨地图。");
    }
}
