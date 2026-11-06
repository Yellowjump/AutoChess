using UnityEditor;
using UnityEngine;
using WorldTimelineDebug;

[CustomEditor(typeof(WorldTimelineRegionCulling))]
public sealed class WorldTimelineRegionCullingEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        var manager=(WorldTimelineRegionCulling)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("运行时区域统计",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("近 / 中 / 远 / 隐藏", $"{manager.NearCount} / {manager.MiddleCount} / {manager.FarCount} / {manager.HiddenCount}");
        EditorGUILayout.LabelField("活动装饰对象",manager.ActiveRendererCount.ToString());
        var instancing = manager.GetComponent<WorldTimelineDecorationInstancing>();
        if (instancing != null)
            EditorGUILayout.LabelField("实例化提交 / 子网格实例", $"{instancing.SubmittedBatches} / {instancing.SubmittedInstances}");
        if (GUILayout.Button("立即刷新显隐"))manager.RefreshVisibility();
        Repaint();
    }
}

[CustomEditor(typeof(WorldTimelineRegion))]
public sealed class WorldTimelineRegionEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var region=(WorldTimelineRegion)target;
        if(GUILayout.Button("重新计算区域包围盒"))
        {
            Undo.RecordObject(region,"Recalculate region bounds");
            region.RecalculateBounds();EditorUtility.SetDirty(region);
            if(PrefabUtility.IsPartOfPrefabInstance(region))PrefabUtility.RecordPrefabInstancePropertyModifications(region);
        }
        if(Application.isPlaying)EditorGUILayout.LabelField("当前层级",region.CurrentTier.ToString());
    }
}
