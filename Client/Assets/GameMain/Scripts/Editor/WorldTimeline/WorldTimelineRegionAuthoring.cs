using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTimelineDebug;
using Object = UnityEngine.Object;

public static class WorldTimelineRegionAuthoring
{
    private const string Folder = "Assets/GameMain/Art/WorldTimeline/BirthRegion";
    private static readonly Vector2Int[] Nodes = {
        new Vector2Int(0,-10), new Vector2Int(1,-10), new Vector2Int(-1,-9), new Vector2Int(0,-9),
        new Vector2Int(-1,-8), new Vector2Int(1,-9), new Vector2Int(0,-8), new Vector2Int(1,-8)
    };
    private static readonly string[] Names = { "青篁谷", "赤石坡", "白溪渡", "黑牙巢", "洗髓灵泉", "古修遗府", "林间岔路", "山口" };

    [MenuItem("Tools/World Timeline/Partition Sample Regions %&F9")]
    public static void PartitionPrefab()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请停止 Play 后再整理区域预制体。");
        string path = Folder + "/BirthRegion.prefab";
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (contents.GetComponentsInChildren<WorldTimelineRegion>(true).Length > 0)
            {
                Debug.Log("样板已经包含独立区域，可直接编辑 Regions 目录中的预制体。");
                return;
            }
            var layout = contents.GetComponent<WorldTimelineMapLayout>();
            if (layout == null) throw new InvalidOperationException("样板缺少地图基准。");
            Directory.CreateDirectory(Folder + "/Regions"); AssetDatabase.Refresh();
            var groups = contents.transform.Cast<Transform>().ToArray();
            var regionObjects = new GameObject[Nodes.Length];
            var near = new Transform[Nodes.Length]; var middle = new Transform[Nodes.Length]; var far = new Transform[Nodes.Length];
            var centers = Nodes.Select(node => layout.Origin + new Vector3(1.73205f * (node.x + node.y * .5f), 0, -1.5f * node.y) * layout.NodeSpacing).ToArray();
            var terrain = AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "/BirthRegionTerrain.asset");
            if (terrain != null)
            {
                var origin = layout.Origin - terrain.bounds.center;
                for (int i = 0; i < centers.Length; i++) centers[i].y = origin.y + terrain.GetInterpolatedHeight(
                    (centers[i].x - origin.x) / terrain.size.x, (centers[i].z - origin.z) / terrain.size.z);
            }
            for (int i = 0; i < Nodes.Length; i++)
            {
                regionObjects[i] = new GameObject(Names[i] + "_区域");
                regionObjects[i].transform.SetParent(contents.transform, false); regionObjects[i].transform.position = centers[i];
                near[i] = Child(regionObjects[i].transform, "Near_小摆件与灌木");
                middle[i] = Child(regionObjects[i].transform, "Middle_树林山石与地标");
                far[i] = Child(regionObjects[i].transform, "Far_地标剪影");
            }
            foreach (var group in groups)
            {
                bool vegetation = group.name.StartsWith("树林_") || group.name.StartsWith("灌木_") || group.name.StartsWith("山石_");
                int landmark = Array.FindIndex(Names, name => group.name.StartsWith(name + "_节点"));
                if (landmark >= 0) { group.SetParent(middle[landmark], true); continue; }
                if (vegetation)
                {
                    foreach (var item in group.Cast<Transform>().ToArray())
                    {
                        int index = Nearest(item.position, centers);
                        bool detail = group.name.StartsWith("灌木_");
                        item.SetParent(detail ? near[index] : middle[index], true);
                    }
                    Object.DestroyImmediate(group.gameObject);
                }
                // Dirt routes are now painted in Terrain. Avoid a redundant spanning mesh draw.
                else if (group.name == "道路_节点间留白" || group.name == "地表_苔草") Object.DestroyImmediate(group.gameObject);
            }
            var regions = new WorldTimelineRegion[Nodes.Length];
            int highRendererCount = 0;
            for (int i = 0; i < Nodes.Length; i++)
            {
                var region = regionObjects[i];
                var staticLandmark = middle[i].Cast<Transform>().FirstOrDefault(t => t.name.StartsWith(Names[i] + "_节点"));
                if (staticLandmark != null)
                {
                    foreach (var item in staticLandmark.Cast<Transform>().ToArray())
                        if (item.name.Contains("泉边石") || item.name.Contains("碎石") || item.name.Contains("倒木")) item.SetParent(near[i], true);
                }
                var renderers = region.GetComponentsInChildren<MeshRenderer>(true);
                highRendererCount += renderers.Length;
                var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                foreach (var renderer in renderers)
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null) { material.enableInstancing = true; EditorUtility.SetDirty(material); }
                }
                IEnumerable<MeshRenderer> candidates = staticLandmark != null
                    ? staticLandmark.GetComponentsInChildren<MeshRenderer>(true)
                    : middle[i].GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.Contains("石") || r.name.Contains("岩"));
                var chosen = candidates.Where(r => r.GetComponent<MeshFilter>()?.sharedMesh != null)
                    .OrderByDescending(r => r.bounds.size.sqrMagnitude).Take(3).ToArray();
                if (chosen.Length == 0) chosen = renderers.OrderByDescending(r => r.bounds.size.sqrMagnitude).Take(1).ToArray();
                foreach (var source in chosen)
                {
                    var proxy = new GameObject(source.name + "_远景"); proxy.transform.SetParent(far[i], false);
                    proxy.transform.position = source.transform.position; proxy.transform.rotation = source.transform.rotation;
                    proxy.transform.localScale = source.transform.lossyScale;
                    proxy.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                    var renderer = proxy.AddComponent<MeshRenderer>(); renderer.sharedMaterials = source.sharedMaterials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
                var localBounds = new Bounds(region.transform.InverseTransformPoint(bounds.center), bounds.size);
                region.AddComponent<WorldTimelineRegion>().Configure(Nodes[i], localBounds,
                    near[i].gameObject, middle[i].gameObject, far[i].gameObject);
                string regionPath = Folder + "/Regions/Region_" + CoordinateName(Nodes[i].x) + "_" + CoordinateName(Nodes[i].y) + ".prefab";
                var asset = PrefabUtility.SaveAsPrefabAsset(region, regionPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, contents.transform);
                instance.transform.localPosition = region.transform.localPosition; instance.transform.localRotation = region.transform.localRotation;
                instance.transform.localScale = region.transform.localScale;
                regions[i] = instance.GetComponent<WorldTimelineRegion>();
                Object.DestroyImmediate(region);
            }
            contents.AddComponent<WorldTimelineRegionCulling>().Configure(regions, layout.NodeSpacing * .3f);
            contents.AddComponent<WorldTimelineDecorationInstancing>();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            AssetDatabase.SaveAssets();
            Debug.Log("轮回区域拆分完成：8 个独立预制体，完整层 " + highRendererCount + " 个 Renderer，远景最多 24 个 Renderer。");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }
    private static Transform Child(Transform parent, string name)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
    private static string CoordinateName(int value) => value < 0 ? "m" + -value : value.ToString();
    private static int Nearest(Vector3 position, Vector3[] centers)
    {
        int best = 0; float distance = float.MaxValue;
        for (int i = 0; i < centers.Length; i++)
        {
            var delta = position - centers[i]; delta.y = 0;
            if (delta.sqrMagnitude < distance) { best = i; distance = delta.sqrMagnitude; }
        }
        return best;
    }
}
