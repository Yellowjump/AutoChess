using System.Collections.Generic;
using UnityEngine;
using WorldTimeline;

namespace WorldTimelineDebug
{
    // Only the map view uses this layer. Battle and title cameras retain their original settings.
    [DefaultExecutionOrder(1000)]
    public sealed class WorldTimelineInkMap : MonoBehaviour
    {
        public const string LayerName = "WorldTimelineMap";
        private const int MapDiameter = 2 * HexWorldMap.Radius + 1;
        private const int ArtworkOrderCount = MapDiameter * MapDiameter;
        private const int ContactShadowOrder = 2 * ArtworkOrderCount + 1;
        private const int LandmarkOrder = ContactShadowOrder + 1;
        private static readonly int WeatherField = Shader.PropertyToID("_TimelineWeatherField");
        private static readonly int WeatherDusk = Shader.PropertyToID("_TimelineWeatherDusk");
        private static readonly int Relief = Shader.PropertyToID("_Relief");
        private static readonly int InkFinish = Shader.PropertyToID("_InkFinish");
        private static readonly int DepthWrite = Shader.PropertyToID("_DepthWrite");
        private static readonly int SoftShadow = Shader.PropertyToID("_SoftShadow");
        private static readonly int RiverWash = Shader.PropertyToID("_RiverWash");
        private static readonly int EmberParticle = Shader.PropertyToID("_EmberParticle");
        private static readonly int TerrainWash = Shader.PropertyToID("_TerrainWash");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color DayBackground = new Color(.92f, .92f, .92f);
        private static readonly Color NightBackground = new Color(.47f, .47f, .47f);
        private float duskVelocity;
        public float Dusk { get; private set; }
        private Camera sceneCamera;
        private int previousMask;
        private CameraClearFlags previousFlags;
        private Color previousBackground;
        private bool applied;
        private WorldTimelineBoard owner;
        private MeshRenderer[] artwork;
        private readonly HashSet<MeshRenderer> hiddenLandmarks = new HashSet<MeshRenderer>();
        private Material contactShadow;
        private Material landmarkMaterial;
        private Material riverMaterial;
        private Material emberMaterial;
        private ParticleSystem volcanoEmbers;
        private Texture2D riverInk;
        private readonly Mesh[] landmarks = new Mesh[8];

        private readonly Dictionary<Vector2Int, List<(MeshRenderer renderer, MeshFilter filter)>> nodeModels = new Dictionary<Vector2Int, List<(MeshRenderer renderer, MeshFilter filter)>>();

        internal MeshFilter VisibleModel(Vector2Int node)
        {
            if (!applied || !nodeModels.TryGetValue(node, out var models)) return null;
            foreach (var model in models)
                if (model.renderer.enabled && !model.renderer.forceRenderingOff &&
                    model.renderer.gameObject.activeInHierarchy && model.filter.sharedMesh != null)
                    return model.filter;
            return null;
        }

        internal Material ContactShadow => contactShadow;
        internal Material LandmarkMaterial => landmarkMaterial;
        internal Mesh GateMesh => landmarks[4];

        public bool IsApplied => applied;
        private void Awake()
        {
            var layout = GetComponent<WorldTimelineMapLayout>();
            var ferryAsset = Resources.Load<GameObject>("WorldTimeline/LinxiFerry");
            var ferryMesh = ferryAsset != null ? ferryAsset.GetComponentInChildren<MeshFilter>()?.sharedMesh : null;
            riverInk = Resources.Load<Texture2D>("WorldTimeline/LinxiRiverInk");
            foreach (var region in GetComponentsInChildren<WorldTimelineRegion>(true))
            {
                var node = region.Node;
                int kind = (node.x, node.y) switch
                {
                    (0, -9) => 0, (4, -9) => 1, (0, -6) => 2, (-4, 9) => 3,
                    (-7, 5) => 5, (-3, 3) => 6, (0, 0) => 7,
                    _ => -1
                };
                foreach (var renderer in region.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.name == "InkArtwork")
                    {
                        // Transparent washes overlap by design. Keep both detail tiers on the
                        // same painter order as the camera pans and zooms across the map.
                        bool localTerrain = renderer.sharedMaterial.GetFloat(TerrainWash) > .5f;
                        int positionOrder = (HexWorldMap.Radius - node.y) * MapDiameter + node.x + HexWorldMap.Radius;
                        renderer.sortingOrder = 1 + positionOrder + (localTerrain ? ArtworkOrderCount : 0);
                        continue;
                    }
                    if (!renderer.name.StartsWith("Landmark_", System.StringComparison.Ordinal)) continue;
                    if (kind < 0)
                    {
                        hiddenLandmarks.Add(renderer);
                        continue;
                    }
                    var anchor = layout.Project(node.x, node.y);
                    if (landmarkMaterial == null)
                    {
                        landmarkMaterial = new Material(renderer.sharedMaterial.shader) { renderQueue = 3010 };
                        landmarkMaterial.SetFloat(Relief, 1f);
                        landmarkMaterial.SetFloat(InkFinish, 1f);
                        landmarkMaterial.SetFloat(DepthWrite, 1f);
                        landmarks[4] = WorldTimelineLandmarkMesh.Create(4);
                    }
                    AddContactShadow(renderer, anchor, layout.NodeSpacing);
                    bool useFerry = kind == 1 && ferryMesh != null;
                    if (useFerry && riverInk != null) AddRiver(renderer, anchor, layout.NodeSpacing);
                    renderer.transform.SetPositionAndRotation(anchor, Quaternion.Euler(0, 337, 0));
                    renderer.transform.localScale = Vector3.one * (layout.NodeSpacing * (useFerry ? .51f : 1f));
                    var model = renderer.GetComponent<MeshFilter>();
                    if (!nodeModels.TryGetValue(node, out var models))
                        nodeModels.Add(node, models = new List<(MeshRenderer, MeshFilter)>());
                    models.Add((renderer, model));
                    model.sharedMesh = useFerry ? ferryMesh
                        : (landmarks[kind] ??= WorldTimelineLandmarkMesh.Create(kind));
                    renderer.sharedMaterial = landmarkMaterial;
                    renderer.sortingOrder = LandmarkOrder;
                    if (kind == 5 && renderer.name == "Landmark_主地标")
                        AddVolcanoEmbers(renderer.transform, layout.NodeSpacing);
                }
            }
        }

        private void AddVolcanoEmbers(Transform volcano, float spacing)
        {
            var effect = new GameObject("熔脉窟 · 火口熔滴") { layer = volcano.gameObject.layer };
            effect.SetActive(false);
            effect.transform.SetParent(volcano.parent, false);
            effect.transform.SetPositionAndRotation(volcano.TransformPoint(WorldTimelineLandmarkMesh.VolcanoVentOffset),
                Quaternion.Euler(-90f, 0f, 0f));
            volcanoEmbers = effect.AddComponent<ParticleSystem>();
            volcanoEmbers.useAutoRandomSeed = false;
            volcanoEmbers.randomSeed = 7619;

            var main = volcanoEmbers.main;
            main.duration = 2.4f;
            main.loop = true;
            main.playOnAwake = true;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            main.maxParticles = 40;
            main.startSpeed = new ParticleSystem.MinMaxCurve(.85f * spacing, 1.35f * spacing);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.52f, .92f);
            main.startSize = new ParticleSystem.MinMaxCurve(.046f * spacing, .105f * spacing);
            main.gravityModifier = 2.55f * spacing / Mathf.Max(Physics.gravity.magnitude, .01f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color32(159, 79, 43, 220), new Color32(230, 158, 98, 255));

            var shape = volcanoEmbers.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 29f;
            shape.radius = .044f * spacing;
            var emission = volcanoEmbers.emission;
            emission.rateOverTime = 2f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(.10f, 13), new ParticleSystem.Burst(1.23f, 7) });
            var colors = volcanoEmbers.colorOverLifetime;
            colors.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .44f), new GradientAlphaKey(0, 1) });
            colors.color = fade;

            emberMaterial = new Material(landmarkMaterial.shader) { renderQueue = 3015 };
            emberMaterial.SetFloat(EmberParticle, 1f);
            var renderer = volcanoEmbers.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = emberMaterial;
            renderer.sortingOrder = LandmarkOrder + 1;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.3f;
            renderer.velocityScale = .013f;
            renderer.maxParticleSize = .035f;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV
            });
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void AddRiver(MeshRenderer landmark, Vector3 anchor, float spacing)
        {
            if (riverMaterial == null)
            {
                riverMaterial = new Material(landmark.sharedMaterial.shader) { renderQueue = 2995 };
                riverMaterial.SetFloat(RiverWash, 1f);
                riverMaterial.SetTexture(BaseMap, riverInk);
            }

            var river = new GameObject("临溪渡口 · 水墨河流") { layer = landmark.gameObject.layer };
            river.transform.SetParent(landmark.transform.parent, false);
            river.transform.SetPositionAndRotation(anchor + Vector3.up * .01f, Quaternion.Euler(0, 337, 0));
            river.transform.localScale = new Vector3(3.2f, 1f, 9.5f) * spacing;
            river.AddComponent<MeshFilter>().sharedMesh = landmark.GetComponent<MeshFilter>().sharedMesh;
            var renderer = river.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = riverMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void AddContactShadow(MeshRenderer standee, Vector3 anchor, float spacing)
        {
            if (contactShadow == null)
            {
                contactShadow = new Material(standee.sharedMaterial.shader) { renderQueue = 3005 };
                contactShadow.SetFloat(SoftShadow, 1f);
                contactShadow.SetColor(BaseColor, new Color(.22f, .18f, .13f, .52f));
            }

            var shadow = new GameObject("Landmark contact shadow") { layer = standee.gameObject.layer };
            shadow.transform.SetParent(standee.transform.parent, false);
            shadow.transform.SetPositionAndRotation(anchor + Vector3.up * .07f, Quaternion.identity);
            shadow.transform.localScale = new Vector3(2.65f, 1f, 1.58f) * spacing;
            shadow.AddComponent<MeshFilter>().sharedMesh = standee.GetComponent<MeshFilter>().sharedMesh;
            var renderer = shadow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = contactShadow;
            renderer.sortingOrder = ContactShadowOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void LateUpdate()
        {
            if (!applied || sceneCamera == null) return;
            if (volcanoEmbers != null)
            {
                bool showEmbers = owner.CaptureView().Zoom < .65f;
                if (volcanoEmbers.gameObject.activeSelf != showEmbers)
                    volcanoEmbers.gameObject.SetActive(showEmbers);
            }
            float target = owner.PlayerDusk;
            if (Dusk != target)
            {
                float next = Mathf.SmoothDamp(Dusk, target, ref duskVelocity, .28f,
                    Mathf.Infinity, Time.unscaledDeltaTime);
                if (Mathf.Abs(next - target) < .001f)
                {
                    next = target;
                    duskVelocity = 0f;
                }
                ApplyDusk(next);
            }
        }
        private void OnEnable()
        {
            artwork = GetComponentsInChildren<MeshRenderer>(true);
            SetSceneryVisible(false);
            var board = WorldTimelineBoard.ForRenderingCamera(Camera.main);
            if (board != null && board.InkMap == this) EnterMap(board, Camera.main);
        }
        private void SetSceneryVisible(bool visible)
        {
            if (volcanoEmbers != null && !visible) volcanoEmbers.gameObject.SetActive(false);
            if (artwork == null) return;
            foreach (var renderer in artwork)
                if (renderer != null) renderer.forceRenderingOff = !visible || hiddenLandmarks.Contains(renderer);
        }
        internal void EnterMap(WorldTimelineBoard requester, Camera camera)
        {
            if (applied && owner == requester && sceneCamera == camera) return;
            Restore();
            int layer = LayerMask.NameToLayer(LayerName);
            if (!isActiveAndEnabled || camera == null || layer < 0) return;
            owner = requester;
            sceneCamera = camera;
            previousMask = camera.cullingMask;
            previousFlags = camera.clearFlags;
            previousBackground = camera.backgroundColor;
            camera.cullingMask = 1 << layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            Shader.SetGlobalVector(WeatherField, requester.WeatherField);
            duskVelocity = 0f;
            ApplyDusk(requester.PlayerDusk);
            SetSceneryVisible(true);
            applied = true;
        }

        private void ApplyDusk(float value)
        {
            Dusk = value;
            Shader.SetGlobalFloat(WeatherDusk, value);
            sceneCamera.backgroundColor = Color.Lerp(DayBackground, NightBackground, value);
        }
        internal void ExitMap(WorldTimelineBoard requester)
        {
            if (owner == requester) Restore();
        }

        private void Restore()
        {
            SetSceneryVisible(false);
            if (!applied) return;
            Shader.SetGlobalVector(WeatherField, Vector4.zero);
            Shader.SetGlobalFloat(WeatherDusk, 0f);
            if (sceneCamera != null)
            {
                sceneCamera.cullingMask = previousMask;
                sceneCamera.clearFlags = previousFlags;
                sceneCamera.backgroundColor = previousBackground;
            }
            applied = false;
            owner = null;
        }
        private void OnDisable()
        {
            Restore();
        }
        private void OnDestroy()
        {
            if (contactShadow != null) Destroy(contactShadow);
            if (landmarkMaterial != null) Destroy(landmarkMaterial);
            if (riverMaterial != null) Destroy(riverMaterial);
            if (emberMaterial != null) Destroy(emberMaterial);
            foreach (var mesh in landmarks)
                if (mesh != null) Destroy(mesh);
        }
    }
}
