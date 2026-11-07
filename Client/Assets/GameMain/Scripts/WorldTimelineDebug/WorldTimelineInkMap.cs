using System.Collections.Generic;
using UnityEngine;

namespace WorldTimelineDebug
{
    // Only the map view uses this layer. Battle and title cameras retain their original settings.
    [DefaultExecutionOrder(1000)]
    public sealed class WorldTimelineInkMap : MonoBehaviour
    {
        public const string LayerName = "WorldTimelineMap";
        private static readonly int WeatherField = Shader.PropertyToID("_TimelineWeatherField");
        private static readonly int WeatherDusk = Shader.PropertyToID("_TimelineWeatherDusk");
        private static readonly Color DayBackground = new Color(.94f, .92f, .85f);
        private static readonly Color NightBackground = new Color(.44f, .47f, .54f);
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
        private readonly List<Transform> standees = new List<Transform>();

        public bool IsApplied => applied;
        private void Awake()
        {
            foreach (var region in GetComponentsInChildren<WorldTimelineRegion>(true))
            {
                var node = region.Node;
                // Village, ferry, mountain pass and shrine are the current map's visual anchors.
                bool keyLandmark = node == new Vector2Int(0, -9) || node == new Vector2Int(4, -9)
                    || node == new Vector2Int(0, -6) || node == new Vector2Int(-4, 9);
                foreach (var renderer in region.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!renderer.name.StartsWith("Landmark_", System.StringComparison.Ordinal)) continue;
                    if (!keyLandmark)
                    {
                        hiddenLandmarks.Add(renderer);
                        continue;
                    }
                    var standee = renderer.transform;
                    standee.localScale *= .65f;
                    standee.position += Vector3.up * standee.TransformVector(Vector3.forward).magnitude * .5f;
                    standees.Add(standee);
                }
            }
        }

        private void LateUpdate()
        {
            if (!applied || sceneCamera == null) return;
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
            foreach (var standee in standees)
            {
                if (!standee.gameObject.activeInHierarchy) continue;
                Vector3 facing = sceneCamera.orthographic ? -sceneCamera.transform.forward
                    : sceneCamera.transform.position - standee.position;
                facing.y = 0;
                if (facing.sqrMagnitude < .0001f) continue;
                standee.rotation = Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(90, 0, 0);
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
    }
}
