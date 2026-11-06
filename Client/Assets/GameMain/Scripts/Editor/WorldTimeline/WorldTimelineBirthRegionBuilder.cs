using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using WorldTimelineDebug;

// Editor-only authoring. The resulting scene contains editable prefab instances and mesh assets.
public static class WorldTimelineBirthRegionBuilder
{
    private const string ScenePath = "Assets/GameMain/Scenes/terrain.unity";
    private const string Output = "Assets/GameMain/Art/WorldTimeline/BirthRegion";
    private const string RootName = "轮回样板区_青篁谷";
    private const string Nature = "Assets/AssetsLocal/LowpolyNatureBundle/Prefab/";
    private const string Dungeon = "Assets/AssetsLocal/LowPolyDungeonsLite/Prefabs/";
    private static readonly Vector2Int[] Coordinates = {
        new Vector2Int(0,-10), new Vector2Int(1,-10), new Vector2Int(-1,-9), new Vector2Int(0,-9),
        new Vector2Int(-1,-8), new Vector2Int(1,-9), new Vector2Int(0,-8), new Vector2Int(1,-8)
    };
    private static readonly string[] Names = { "青篁谷", "赤石坡", "白溪渡", "黑牙巢", "洗髓灵泉", "古修遗府", "林间岔路", "山口" };
    private static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();
    private static Terrain terrain;
    private static Collider ground;
    private static Vector3 origin;
    private static float unit;
    private static Vector3[] sites;
    private static Transform root;
    private static System.Random random;

    [MenuItem("Tools/World Timeline/Build Birth Region Sample %&F8")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请停止 Play 后再制作样板区。");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        GameObject draft = null;
        try
        {
            Physics.SyncTransforms();
            terrain = scene.GetRootGameObjects().Where(go => go.name != RootName)
                .SelectMany(go => go.GetComponentsInChildren<Terrain>(true)).Where(t => t.terrainData != null)
                .OrderByDescending(t => t.name.StartsWith("Terrain_2_2_"))
                .ThenByDescending(t => t.terrainData.size.x * t.terrainData.size.z)
                .ThenBy(t => t.transform.position.x).ThenBy(t => t.transform.position.z).FirstOrDefault();
            Bounds bounds;
            if (terrain != null)
            {
                bounds = terrain.terrainData.bounds;
                bounds.center += terrain.GetPosition();
            }
            else
            {
                ground = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Where(c => c.enabled && !c.isTrigger && c.gameObject.scene == scene)
                    .OrderByDescending(c => c.bounds.size.x * c.bounds.size.z).FirstOrDefault();
                if (ground == null) throw new InvalidOperationException("terrain 场景没有可用地形或地面碰撞体。");
                bounds = ground.bounds;
            }
            origin = bounds.center;
            unit = Mathf.Min(bounds.size.x, bounds.size.z) * .68f / (1.73205f * 21);
            if (unit <= .01f) throw new InvalidOperationException("地形尺寸无效。");
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            Materials.Clear(); random = new System.Random(7619);
            draft = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(draft, scene);
            root = draft.transform;
            var layout = draft.AddComponent<WorldTimelineMapLayout>();
            layout.Origin = origin; layout.NodeSpacing = unit;
            layout.SourceTerrainName = terrain != null ? terrain.name : "";
            SculptTerrain();
            sites = Coordinates.Select(c => Point(c.x,c.y)).ToArray();
            if (terrain == null) MakeGround();
            MakePaths();
            MakeForest();
            MakeLandmarks();
            var keyLight = new GameObject("样板区日光");
            keyLight.transform.SetParent(root, false);
            keyLight.transform.rotation = Quaternion.Euler(48,-35,0);
            var sun = keyLight.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.3f; sun.color = new Color(1,.95f,.84f);
            sun.shadows = LightShadows.Soft;
            foreach (var collider in draft.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            foreach (var renderer in draft.GetComponentsInChildren<Renderer>())
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            var sceneSuns = scene.GetRootGameObjects().Where(go => go != draft && go.name != RootName)
                .SelectMany(go => go.GetComponentsInChildren<Light>(true)).Where(light => light.type == LightType.Directional).ToArray();
            foreach (var light in sceneSuns)
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(48,-35,0);
                    light.color = new Color(1,.94f,.83f);
                    light.intensity = 1.15f;
                    light.lightmapBakeType = LightmapBakeType.Realtime;
                    light.shadows = LightShadows.Soft;
                    light.enabled = true;
                    light.gameObject.SetActive(true);
                }
            if (sceneSuns.Length > 0) Object.DestroyImmediate(keyLight);
            string path = Output + "/BirthRegion.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(draft, path);
            Object.DestroyImmediate(draft); draft = null;
            WorldTimelineRegionAuthoring.PartitionPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var old in scene.GetRootGameObjects().Where(go => go.name == RootName).ToArray()) Object.DestroyImmediate(old);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = RootName;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Preview(scene);
            File.WriteAllText(Path.Combine(Output,"BuildReport.txt"),
                "Birth-region sample baked into terrain.unity\n8 locations; unit=" + unit + "\norigin=" + origin +
                "\nTerrain=" + (terrain != null ? terrain.name : "collider fallback") + "\nRenderers=" + instance.GetComponentsInChildren<Renderer>().Length + "\nPrefab=" + path);
            AssetDatabase.Refresh();
            Debug.Log("轮回样板区已完成：" + path + "，8 个节点。节点中心及道路已留空。");
        }
        finally
        {
            if (draft != null) Object.DestroyImmediate(draft);
            if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void SculptTerrain()
    {
        if (terrain == null) return;
        var sourceTerrain = PrefabUtility.GetCorrespondingObjectFromSource(terrain);
        var source = sourceTerrain != null ? sourceTerrain.terrainData : terrain.terrainData;
        string path = Output + "/BirthRegionTerrain.asset";
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if (data == null) { data=Object.Instantiate(source); AssetDatabase.CreateAsset(data,path); }
        else EditorUtility.CopySerialized(source,data);
        terrain.terrainData=data;
        var collider=terrain.GetComponent<TerrainCollider>();if(collider!=null)collider.terrainData=data;
        PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
        if(collider!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        var centers=Coordinates.Select(c=>Point(c.x,c.y)).ToArray();
        var pools=new[]{At(centers[2],-.35f,.38f),At(centers[4],-.35f,.38f)};
        pools[0].y=centers[2].y; pools[1].y=centers[4].y;
        int resolution=data.heightmapResolution;
        var heights=data.GetHeights(0,0,resolution,resolution);
        var basePos=terrain.GetPosition();
        for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
        {
            var p=basePos+new Vector3(x*data.size.x/(resolution-1),0,z*data.size.z/(resolution-1));
            float h=heights[z,x]*data.size.y+basePos.y;
            foreach(var center in centers)
            {
                float d=FlatDistance(p,center)/unit;
                float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.24f,.68f,d));
                h=Mathf.Lerp(h,center.y,blend);
            }
            foreach(var pool in pools)
            {
                float d=FlatDistance(p,pool)/unit;
                float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.28f,.42f,d));
                h=Mathf.Lerp(h,pool.y-unit*.035f,blend);
            }
            heights[z,x]=Mathf.Clamp01((h-basePos.y)/data.size.y);
        }
        data.SetHeights(0,0,heights);
        var layers=data.terrainLayers.ToList();
        var grass=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/GameMain/Scenes/terrain/Grass_A.terrainlayer");
        var soil=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/GameMain/Scenes/terrain/Grass_Soil_A.terrainlayer");
        if(grass!=null&&soil!=null)
        {
            if(!layers.Contains(grass))layers.Add(grass);if(!layers.Contains(soil))layers.Add(soil);
            data.terrainLayers=layers.ToArray();int gi=layers.IndexOf(grass),si=layers.IndexOf(soil);
            sites=centers;
            int x0=Mathf.Clamp(Mathf.FloorToInt((centers.Min(v=>v.x)-unit*1.2f-basePos.x)/data.size.x*data.alphamapWidth),0,data.alphamapWidth-1);
            int z0=Mathf.Clamp(Mathf.FloorToInt((centers.Min(v=>v.z)-unit*1.2f-basePos.z)/data.size.z*data.alphamapHeight),0,data.alphamapHeight-1);
            int x1=Mathf.Clamp(Mathf.CeilToInt((centers.Max(v=>v.x)+unit*1.2f-basePos.x)/data.size.x*data.alphamapWidth),x0+1,data.alphamapWidth);
            int z1=Mathf.Clamp(Mathf.CeilToInt((centers.Max(v=>v.z)+unit*1.2f-basePos.z)/data.size.z*data.alphamapHeight),z0+1,data.alphamapHeight);
            var paint=data.GetAlphamaps(x0,z0,x1-x0,z1-z0);
            for(int z=0;z<z1-z0;z++)for(int x=0;x<x1-x0;x++)
            {
                var p=basePos+new Vector3((x+x0+.5f)/data.alphamapWidth*data.size.x,0,(z+z0+.5f)/data.alphamapHeight*data.size.z);
                float distance=centers.Min(c=>FlatDistance(c,p))/unit;
                float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.77f,1.15f,distance));
                float dirt=Road(p,unit*.09f)||distance<.20f?.85f:0;
                for(int l=0;l<layers.Count;l++)paint[z,x,l]*=1-weight;
                paint[z,x,gi]+=weight*(1-dirt);paint[z,x,si]+=weight*dirt;
            }
            data.SetAlphamaps(x0,z0,paint);
        }
        terrain.Flush();EditorUtility.SetDirty(data);
    }

    private static float Height(Vector3 p)
    {
        if (terrain != null)
        {
            var local=p-terrain.GetPosition();var data=terrain.terrainData;
            return data.GetInterpolatedHeight(local.x/data.size.x,local.z/data.size.z)+terrain.GetPosition().y;
        }
        var b = ground.bounds;
        return ground.Raycast(new Ray(new Vector3(p.x,b.max.y+50,p.z),Vector3.down),out var hit,b.size.y+100)
            ? hit.point.y : b.max.y;
    }
    private static Vector3 Point(float q,float r)
    {
        var p = origin + new Vector3(1.73205f*(q+r*.5f),0,-1.5f*r)*unit;
        p.y = Height(p); return p;
    }
    private static Vector3 At(Vector3 center,float x,float z)
    {
        var p=center+new Vector3(x,0,z)*unit;p.y=Height(p);return p;
    }
    private static float Next(float min,float max) => min+(float)random.NextDouble()*(max-min);
    private static Transform Group(string name)
    { var go=new GameObject(name);go.transform.SetParent(root,false);return go.transform; }
    private static float FlatDistance(Vector3 a,Vector3 b) => Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
    private static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
    {
        p.y=a.y=b.y=0;var ab=b-a;
        return Vector3.Distance(p,a+ab*Mathf.Clamp01(Vector3.Dot(p-a,ab)/ab.sqrMagnitude));
    }
    private static bool Road(Vector3 p,float width)
    {
        for(int i=0;i<sites.Length;i++)for(int j=i+1;j<sites.Length;j++)
            if(FlatDistance(sites[i],sites[j])<unit*1.8f && SegmentDistance(p,sites[i],sites[j])<width)return true;
        return false;
    }
    private static Material Surface(string name,string layerName,Color tint)
    {
        string path=Output+"/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/GameMain/Scenes/terrain/"+layerName+".terrainlayer");
        material.SetTexture("_BaseMap",layer!=null?layer.diffuseTexture:null);
        material.SetColor("_BaseColor",tint);material.SetFloat("_Smoothness",.05f);material.enableInstancing=true;
        return material;
    }
    private static void MeshAsset(string name,List<Vector3> vertices,List<int> triangles,Material material,Transform parent)
    {
        string path=Output+"/"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};AssetDatabase.CreateAsset(mesh,path);}
        else mesh.Clear();
        mesh.indexFormat=IndexFormat.UInt32;
        mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
        mesh.SetUVs(0,vertices.Select(v=>new Vector2(v.x,v.z)/(unit*.28f)).ToList());mesh.RecalculateNormals();mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    private static void MakeGround()
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();
        float step=unit*.1f;
        float x0=sites.Min(p=>p.x)-unit*1.05f,x1=sites.Max(p=>p.x)+unit*1.05f;
        float z0=sites.Min(p=>p.z)-unit*1.05f,z1=sites.Max(p=>p.z)+unit*1.05f;
        for(float z=z0;z<z1;z+=step)for(float x=x0;x<x1;x+=step)
        {
            var center=new Vector3(x+step*.5f,0,z+step*.5f);
            float edge=unit*(.9f+.10f*Mathf.PerlinNoise(x/unit*2,z/unit*2));
            if(sites.Min(p=>FlatDistance(center,p))>edge)continue;
            int start=vertices.Count;
            foreach(var p in new[]{new Vector3(x,0,z),new Vector3(x,0,z+step),new Vector3(x+step,0,z+step),new Vector3(x+step,0,z)})
                vertices.Add(new Vector3(p.x,Height(p)+.035f,p.z));
            triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        MeshAsset("MossyValleyGround",vertices,triangles,Surface("MossGrass","Grass_Moss_A",new Color(.76f,.91f,.72f)),Group("地表_苔草"));
    }
    private static void MakePaths()
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int i=0;i<sites.Length;i++)for(int j=i+1;j<sites.Length;j++)
        {
            if(FlatDistance(sites[i],sites[j])>unit*1.8f)continue;
            var a=sites[i];var b=sites[j];var side=Vector3.Cross((b-a).normalized,Vector3.up).normalized*unit*.065f;
            int start=vertices.Count;
            for(int k=0;k<=24;k++)
            {
                var p=Vector3.Lerp(a,b,k/24f);var bend=side*Mathf.Sin(k/24f*Mathf.PI)*.55f;
                foreach(var v in new[]{p+bend-side,p+bend+side})vertices.Add(new Vector3(v.x,Height(v)+.085f,v.z));
                if(k>0){int n=start+k*2;triangles.AddRange(new[]{n-2,n-1,n,n-1,n+1,n});}
            }
        }
        MeshAsset("ValleyFootpaths",vertices,triangles,Surface("EarthPath","Grass_Soil_A",new Color(.9f,.85f,.72f)),Group("道路_节点间留白"));
    }
    private static GameObject Place(string asset,string name,Vector3 position,float height,float yaw,Transform parent)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(asset);
        if(prefab==null)throw new FileNotFoundException(asset);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.name=name;
        go.transform.rotation=Quaternion.Euler(0,yaw,0);go.transform.position=position;
        var renderers=go.GetComponentsInChildren<Renderer>();
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        float sizeScale = height/Mathf.Max(.01f,bounds.size.y);
        if (asset.Contains("Rock_")) sizeScale = Mathf.Min(sizeScale, height*1.85f/Mathf.Max(bounds.size.x,bounds.size.z));
        go.transform.localScale*=sizeScale;
        bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        go.transform.position+=Vector3.up*(position.y-bounds.min.y);
        foreach(var r in renderers)
        {
            r.sharedMaterials=r.sharedMaterials.Select(source=>
            {
                if(source==null)return source;
                if(Materials.TryGetValue(source,out var cached))return cached;
                string path=Output+"/Model_"+source.name.Replace('/','_')+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null)
                {
                    mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    var texture=source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):source.mainTexture;
                    mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.08f);
                    mat.enableInstancing=true;AssetDatabase.CreateAsset(mat,path);
                }
                Materials.Add(source,mat);return mat;
            }).ToArray();
        }
        return go;
    }
    private static void MakeForest()
    {
        var forest=Group("树林_节点与道路避让");var rocks=Group("山石_区域边界");var bushes=Group("灌木_林缘");
        for(int i=0;i<sites.Length;i++)
        {
            int planted=0;
            for(int attempt=0;attempt<180&&planted<14;attempt++)
            {
                float angle=Next(0,Mathf.PI*2),radius=Next(.48f,.93f);
                var p=At(sites[i],Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);
                if(sites.Any(s=>FlatDistance(p,s)<unit*.42f)||Road(p,unit*.18f))continue;
                Place(Nature+"SpringTree_0"+(planted%3+3)+".prefab","谷地树",p,unit*Next(.25f,.43f),Next(0,360),forest);
                if(planted%2==0)Place(Nature+"Bush_0"+(planted%4+1)+".prefab","林缘灌木",At(p,.09f,.08f),unit*Next(.055f,.10f),Next(0,360),bushes);
                planted++;
            }
            for(int k=0;k<4;k++)
            {
                float angle=Next(0,Mathf.PI*2);var p=At(sites[i],Mathf.Cos(angle)*.9f,Mathf.Sin(angle)*.9f);
                if(!Road(p,unit*.22f))Place(Nature+"Rock_0"+(k%3+3)+".prefab","谷边山石",p,unit*Next(.18f,.4f),Next(0,360),rocks);
            }
        }
    }
    private static void MakeLandmarks()
    {
        for(int i=0;i<sites.Length;i++)
        {
            var group=Group(Names[i]+"_节点("+Coordinates[i].x+","+Coordinates[i].y+")");
            if(i==3)
            {
                Place(Nature+"Rock_06.prefab","巢穴后壁",At(sites[i],0,.60f),unit*.72f,20,group);
                Place(Nature+"Rock_04.prefab","洞口左岩",At(sites[i],-.35f,.38f),unit*.43f,25,group);
                Place(Nature+"Rock_05.prefab","洞口右岩",At(sites[i],.35f,.38f),unit*.40f,-20,group);
                var lintel=Place(Nature+"Rock_03.prefab","洞口覆岩",At(sites[i],0,.40f),unit*.20f,85,group);
                lintel.transform.localScale=new Vector3(lintel.transform.localScale.x*2.2f,lintel.transform.localScale.y,lintel.transform.localScale.z);
                lintel.transform.position+=Vector3.up*unit*.30f;
            }
            else if(i==5)
            {
                for(int k=0;k<4;k++)Place(Dungeon+"Column_01.prefab","古修残柱",At(sites[i],(k%2==0?-.38f:.38f),.32f+(k/2)*.38f),unit*(k==1?.18f:.34f),k*90,group);
                Place(Dungeon+"Wall_27.prefab","遗府残墙",At(sites[i],0,.78f),unit*.28f,0,group);
                Place(Nature+"Rock_02.prefab","遗迹碎石",At(sites[i],.5f,.3f),unit*.14f,40,group);
            }
            else if(i==4 || i==2)
            {
                var center=At(sites[i],-.35f,.38f);float radius=unit*(i==4?.30f:.22f);
                var vertices=new List<Vector3>{center+Vector3.up*(unit*.04f)};var triangles=new List<int>();
                for(int k=0;k<=32;k++){float angle=k*Mathf.PI/16;vertices.Add(center+new Vector3(Mathf.Cos(angle)*radius,unit*.04f,Mathf.Sin(angle)*radius));if(k>0)triangles.AddRange(new[]{0,k+1,k});}
                MeshAsset("SpringPool_"+i,vertices,triangles,Surface("JadeSpring","",new Color(.12f,.60f,.58f)),group);
                for(int k=0;k<9;k++){float a=k*Mathf.PI*2/9;Place(Nature+"Rock_01.prefab","泉边石",At(center,Mathf.Cos(a)*radius/unit,Mathf.Sin(a)*radius/unit),unit*.07f,k*40,group);}
            }
            else if(i==0)
            {
                Place(Nature+"Rock_04.prefab","谷口石",At(sites[i],-.48f,.30f),unit*.28f,15,group);
                Place(Nature+"FallenTree_01.prefab","林间倒木",At(sites[i],.48f,.25f),unit*.09f,70,group);
            }
        }
    }
    private static void Preview(Scene scene)
    {
        var go=new GameObject("Sample preview camera (temporary)");
        SceneManager.MoveGameObjectToScene(go,scene);
        var camera=go.AddComponent<Camera>();camera.orthographic=false;camera.fieldOfView=48;
        camera.transform.rotation=Quaternion.Euler(48,-20,0);
        var center=sites.Aggregate(Vector3.zero,(a,b)=>a+b)/sites.Length;
        camera.transform.position=center-camera.transform.forward*unit*10;
        camera.nearClipPlane=.3f;camera.farClipPlane=unit*60;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.24f,.34f,.39f);
        var rt=new RenderTexture(1600,1000,24);rt.Create();
        try
        {
            camera.targetTexture=rt;
            camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            File.WriteAllBytes(Output+"/BirthRegionPreview.png",image.EncodeToPNG());
            Object.DestroyImmediate(image);RenderTexture.active=previous;
        }
        finally { camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go); }
    }
}
