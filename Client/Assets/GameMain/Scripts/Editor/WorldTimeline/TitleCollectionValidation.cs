using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WorldTimelineDebug;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TitleCollectionValidation
{
    private const string Pending = "TitleCollectionValidation.Pending";
    private static double started;
    private static IEnumerator inspection;
    static TitleCollectionValidation()
    {
        if (SessionState.GetBool(Pending,false)) EditorApplication.update += Tick;
    }

    public static void RunBatch()
    {
        SessionState.SetBool(Pending,true);
        EditorSceneManager.OpenScene("Assets/GameMain/Scenes/bootstrap.unity");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        try
        {
            if (inspection != null)
            {
                if (!inspection.MoveNext()) Finish(0);
                return;
            }
            var title = Object.FindFirstObjectByType<MainTitlePanelCtrl>();
            if (title == null || !title.Visible)
            {
                // Batch mode does not send WaitForEndOfFrame to the framework's startup coroutine.
                if (Application.isBatchMode && Object.FindFirstObjectByType<UnityGameFramework.Runtime.ProcedureComponent>() is { } procedure &&
                    GameFramework.GameFrameworkEntry.GetModule<GameFramework.Fsm.IFsmManager>()
                        .HasFsm<GameFramework.Procedure.IProcedureManager>() && procedure.CurrentProcedure == null)
                {
                    procedure.StopAllCoroutines();
                    GameFramework.GameFrameworkEntry.GetModule<GameFramework.Procedure.IProcedureManager>()
                        .StartProcedure<Procedure.ProcedurePreload>();
                }
                if (EditorApplication.timeSinceStartup-started > 180) throw new TimeoutException("Title did not open.");
                return;
            }
            inspection = Inspect(title);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    private static IEnumerator Inspect(MainTitlePanelCtrl title)
    {
        string directory = Path.Combine(Path.GetTempPath(),"TitleCollection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        WorldTimelineDebugView.EditorSaveDirectory = directory;
        try
        {
            File.WriteAllText(Path.Combine(directory,"collection.json"),
                "{\"entries\":[\"monster:GateGolem\",\"monster:WreckBeast\",\"monster:BlackfangBadger\",\"gear:1\",\"gear:2\",\"gear:3\"]}");
            title.transform.Find("CollectionButton").GetComponent<Button>().onClick.Invoke();
            var panel = GameObject.Find("TitleCollection");
            RectTransform Page() => panel.GetComponentsInChildren<RectTransform>().Single(r => r.name == "CollectionPage");
            void Click(string name) => Page().Find(name).GetComponent<Button>().onClick.Invoke();
            void CheckMask(int expected)
            {
                if (Page().GetComponentsInChildren<Image>().Count(i => i.name == "LockedMask") != expected)
                    throw new InvalidOperationException("Unexpected mask count.");
            }
            CheckMask(3);
            if (Page().Find("Guardian").GetComponent<RawImage>().texture == null) throw new Exception("Missing atlas");
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 8; i++) yield return null;
            Capture(panel.GetComponent<Canvas>(), "CollectionMonsters");
            for (int i = 0; i < 8; i++) yield return null;
            string original = Page().Find("PlateName").GetComponent<TMPro.TMP_Text>().text;
            Click("Select1");
            if (Page().Find("PlateName").GetComponent<TMPro.TMP_Text>().text == original)
                throw new Exception("Selecting another specimen did not update the plate.");
            Click("Select3");
            if (Page().Find("PlateDescription") != null) throw new Exception("Locked description was exposed.");
            Click("Next");
            CheckMask(6);
            Click("Items");
            CheckMask(3);
            if (Page().Find("Equipment").GetComponent<Image>().sprite == null) throw new Exception("Missing item icon");
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 8; i++) yield return null;
            Capture(panel.GetComponent<Canvas>(), "CollectionItems");
            for (int i = 0; i < 8; i++) yield return null;
            Click("Close");
            if (!title.Visible) throw new Exception("Title was not restored.");
            Debug.Log("TITLE_COLLECTION_VALIDATION_PASS");
        }
        finally
        {
            WorldTimelineDebugView.EditorSaveDirectory = null;
            File.Delete(Path.Combine(directory,"collection.json"));
            Directory.Delete(directory);
        }
    }

    private static void Capture(Canvas canvas,string name)
    {
        var camera = new GameObject("ValidationCamera").AddComponent<Camera>();
        var target = RenderTexture.GetTemporary(1440,810,24);
        var image = new Texture2D(1440,810,TextureFormat.RGB24,false);
        var previous = RenderTexture.active;
        try
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            camera.cullingMask = 1 << 5;
            camera.targetTexture = target;
            canvas.gameObject.layer = 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,1440,810),0,0);
            image.Apply();
            File.WriteAllBytes(Path.Combine("Library",name+".png"),image.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(image);
        }
    }

    private static void Finish(int code)
    {
        SessionState.EraseBool(Pending);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
