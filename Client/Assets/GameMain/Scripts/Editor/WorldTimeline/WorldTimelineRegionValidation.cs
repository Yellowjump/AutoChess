using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WorldTimelineDebug;
using WorldTimeline;
using Object = UnityEngine.Object;

public static class WorldTimelineRegionValidation
{
    [Serializable] private sealed class Sample
    {
        public string view;
        public Vector3 cameraPosition; public float orthographicSize, fieldOfView; public bool orthographic;
        public int near, middle, far, hidden, activeRenderers, drawCalls, batches, triangles, nodes, year;
    }
    [Serializable] private sealed class Report { public bool passed, hysteresisPassed, edgeBufferPassed, perspectiveInputPassed;
        public float zoomDistanceRatio; public Vector3 panScreenDelta;
        public int referencedMeshes, referencedMaterials; public long sharedMeshBytes, sharedMaterialBytes; public List<Sample> samples = new List<Sample>(); }
    private static WorldTimelineBoard board;
    private static WorldTimelineRegionCulling manager;
    private static WorldTimelineDebugView session;
    private static Vector3 savedFocus;
    private static float savedZoom;
    private static int stage;
    private static double nextSample;
    private static Report report;
    private static float inputDistance;
    private static Vector3 inputFocus, inputScreen;
    private static string ReportFolder => Path.GetFullPath(Path.Combine(Application.dataPath,"../../Tests/WorldTimeline/obj/RegionVisibility"));

    [MenuItem("Tools/World Timeline/Validate Region Visibility %&F10")]
    public static void Validate()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("请先运行并进入轮回地图。");
        if (report != null) throw new InvalidOperationException("区域验证正在进行。");
        board = Object.FindFirstObjectByType<WorldTimelineBoard>();
        manager = Object.FindFirstObjectByType<WorldTimelineRegionCulling>();
        session = Object.FindFirstObjectByType<WorldTimelineDebugView>();
        if (board == null || manager == null || session == null || session.IsBusy)
            throw new InvalidOperationException("需要处于空闲的轮回地图，且样板已完成区域拆分。");
        var saved = board.CaptureView(); savedFocus = saved.Focus; savedZoom = saved.Zoom;
        report = new Report();
        var meshes=manager.GetComponentsInChildren<MeshFilter>(true).Select(filter=>filter.sharedMesh).Where(mesh=>mesh!=null).Distinct().ToArray();
        var materials=manager.GetComponentsInChildren<Renderer>(true).SelectMany(renderer=>renderer.sharedMaterials).Where(material=>material!=null).Distinct().ToArray();
        report.referencedMeshes=meshes.Length;report.referencedMaterials=materials.Length;
        report.sharedMeshBytes=meshes.Sum(mesh=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh));
        report.sharedMaterialBytes=materials.Sum(material=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(material));
        stage = 0; Directory.CreateDirectory(ReportFolder);
        MoveCamera(); EditorApplication.update += Tick;
    }
    [MenuItem("Tools/World Timeline/Focus Sample Region %&F11")]
    public static void FocusSample()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("请先进入轮回地图。");
        var map=Object.FindFirstObjectByType<WorldTimelineBoard>();
        if (map==null) throw new InvalidOperationException("轮回地图尚未创建。");
        map.Focus(0,-9); map.Zoom(.4f);
    }
    private static void MoveCamera()
    {
        if (stage == 0) { board.Focus(0,-9); board.Zoom(.4f); }
        if (stage == 1) board.Focus(0,-9);
        if (stage == 2) { board.Overview(); manager.enabled=false; }
        if (stage == 3) manager.enabled=true;
        if (stage == 4) { board.Focus(10,5); board.Zoom(.4f); }
        if (stage == 5) board.Focus(0,-9);
        if (stage == 6) board.Zoom(.8f);
        if (stage == 7) board.Pan(new Vector2(4,4));
        nextSample = EditorApplication.timeSinceStartup + 1.2;
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || board == null || manager == null) { Finish(false); return; }
        if (EditorApplication.timeSinceStartup < nextSample) return;
        try
        {
            if (stage != 2) manager.RefreshVisibility();
            var regions=manager.Regions;
            report.samples.Add(new Sample {
                view = new[]{"near","middle","far-full-baseline","far","outside","input-focus","input-zoom","input-pan"}[stage],
                cameraPosition=Camera.main.transform.position, orthographicSize=Camera.main.orthographicSize, fieldOfView=Camera.main.fieldOfView, orthographic=Camera.main.orthographic,
                near=regions.Count(r=>r.CurrentTier==RegionDetailTier.Near),
                middle=regions.Count(r=>r.CurrentTier==RegionDetailTier.Middle),
                far=regions.Count(r=>r.CurrentTier==RegionDetailTier.Far),
                hidden=regions.Count(r=>r.CurrentTier==RegionDetailTier.Hidden),
                activeRenderers=regions.Sum(r=>r.ActiveRendererCount), drawCalls = UnityStats.drawCalls,
                batches = UnityStats.batches, triangles = UnityStats.triangles,
                nodes = session.Simulation.State.Nodes.Count, year = session.Simulation.State.Year
            });
            if(stage==5)
            {
                report.perspectiveInputPassed=!Camera.main.orthographic;
                foreach(var point in new[]{new Vector2Int(0,-9),new Vector2Int(1,-9),new Vector2Int(-1,-8)})
                {
                    int id=HexWorldMap.Id(point.x,point.y);var screen=board.ProjectNode(id);
                    report.perspectiveInputPassed &= screen.z>Camera.main.nearClipPlane && board.Pick(screen)==id;
                }
                inputFocus=board.CaptureView().Focus;
                inputDistance=Vector3.Distance(Camera.main.transform.position,inputFocus);
            }
            if(stage==6)
            {
                report.zoomDistanceRatio=Vector3.Distance(Camera.main.transform.position,inputFocus)/inputDistance;
                report.perspectiveInputPassed &= Mathf.Abs(report.zoomDistanceRatio-.8f)<.01f;
                inputScreen=board.ProjectNode(HexWorldMap.Id(0,-9));
            }
            if(stage==7)
            {
                report.panScreenDelta=board.ProjectNode(HexWorldMap.Id(0,-9))-inputScreen;
                report.perspectiveInputPassed &= Mathf.Abs(report.panScreenDelta.x-4)<1.5f && Mathf.Abs(report.panScreenDelta.y+4)<1.5f;
            }
            stage++;
            if (stage < 8) { MoveCamera(); return; }
            var s = report.samples;
            bool passed = s[0].near > 0 && s[1].middle > 0 && s[3].far == manager.Regions.Length && s[3].near == 0
                && s[4].activeRenderers == 0 && s[4].hidden == manager.Regions.Length
                && s[2].activeRenderers > s[3].activeRenderers
                && s[2].cameraPosition==s[3].cameraPosition && s[2].orthographicSize==s[3].orthographicSize
                && s.All(sample => sample.nodes == 331 && sample.year == s[0].year);
            report.hysteresisPassed = ValidateHysteresis();
            report.edgeBufferPassed = ValidateEdgeBuffer();
            Finish(passed && report.hysteresisPassed && report.edgeBufferPassed && report.perspectiveInputPassed);
        }
        catch (Exception error) { Debug.LogException(error); Finish(false); }
    }
    private static float ProjectedHalfExtent(WorldTimelineRegion region, Camera camera)
    {
        var up=camera.transform.up;var right=camera.transform.right;
        up=new Vector3(Mathf.Abs(up.x),Mathf.Abs(up.y),Mathf.Abs(up.z));
        right=new Vector3(Mathf.Abs(right.x),Mathf.Abs(right.y),Mathf.Abs(right.z));
        return Mathf.Max(Vector3.Dot(up,region.WorldBounds.extents),Vector3.Dot(right,region.WorldBounds.extents)/camera.aspect);
    }
    private static Plane[] UnboundedPlanes()
    {
        return new[]{new Plane(Vector3.right,100000),new Plane(Vector3.left,100000),new Plane(Vector3.up,100000),
            new Plane(Vector3.down,100000),new Plane(Vector3.forward,100000),new Plane(Vector3.back,100000)};
    }
    private static void SetProjectedFraction(Camera camera,WorldTimelineRegion region,float extent,float fraction)
    {
        if(camera.orthographic)camera.orthographicSize=extent/fraction;
        else
        {
            float depth=extent/(fraction*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f));
            camera.transform.position=region.WorldBounds.center-camera.transform.forward*depth;
        }
    }
    private static bool ValidateHysteresis()
    {
        var camera=Camera.main;var region=manager.Regions[0];var planes=UnboundedPlanes();
        float saved=camera.orthographicSize;var savedPosition=camera.transform.position;var tier=region.CurrentTier;
        bool passed=true;
        try
        {
            float extent=ProjectedHalfExtent(region,camera);
            foreach(var initial in new[]{RegionDetailTier.Near,RegionDetailTier.Middle})
            {
                region.Apply(initial);
                for(int i=0;i<20;i++)
                {
                    SetProjectedFraction(camera,region,extent,i%2==0?.179f:.181f);
                    region.Evaluate(camera,planes,Time.unscaledTime,.18f,.065f,.12f,0,0);
                    passed &= region.CurrentTier==initial;
                }
            }
            SetProjectedFraction(camera,region,extent,.23f);
            region.Evaluate(camera,planes,Time.unscaledTime,.18f,.065f,.12f,0,0);
            passed &= region.CurrentTier==RegionDetailTier.Near;
            SetProjectedFraction(camera,region,extent,.14f);
            region.Evaluate(camera,planes,Time.unscaledTime,.18f,.065f,.12f,0,0);
            return passed && region.CurrentTier==RegionDetailTier.Middle;
        }
        finally {camera.orthographicSize=saved;camera.transform.position=savedPosition;region.Apply(tier);}
    }
    private static bool ValidateEdgeBuffer()
    {
        var camera=Camera.main;var region=manager.Regions[0];var planes=UnboundedPlanes();var tier=region.CurrentTier;
        float now=Time.unscaledTime;bool passed;
        try
        {
            region.Apply(RegionDetailTier.Near);
            region.Evaluate(camera,planes,now,.18f,.065f,.12f,5,.35f);
            planes[0]=new Plane(Vector3.left,region.WorldBounds.min.x-4);
            region.Evaluate(camera,planes,now+1,.18f,.065f,.12f,5,.35f);
            passed=region.CurrentTier!=RegionDetailTier.Hidden;
            planes[0]=new Plane(Vector3.left,region.WorldBounds.min.x-6);
            region.Evaluate(camera,planes,now+2,.18f,.065f,.12f,5,.35f);
            region.Evaluate(camera,planes,now+2.2f,.18f,.065f,.12f,5,.35f);
            passed &= region.CurrentTier!=RegionDetailTier.Hidden;
            region.Evaluate(camera,planes,now+2.4f,.18f,.065f,.12f,5,.35f);
            return passed && region.CurrentTier==RegionDetailTier.Hidden;
        }
        finally {region.Apply(tier);}
    }
    private static void Finish(bool passed)
    {
        EditorApplication.update -= Tick;
        if (report == null) return;
        report.passed = passed;
        File.WriteAllText(Path.Combine(ReportFolder,"Validation.json"),JsonUtility.ToJson(report,true));
        if (manager != null) manager.enabled=true;
        if (board != null) board.RestoreView(savedFocus,savedZoom);
        Debug.Log((passed?"PASS":"FAIL")+": 区域近中远与视野剔除验证，报告："+ReportFolder);
        report = null;
    }
}
