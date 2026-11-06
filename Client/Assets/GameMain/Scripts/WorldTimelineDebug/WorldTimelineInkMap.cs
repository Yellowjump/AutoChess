using System.Collections.Generic;
using UnityEngine;

namespace WorldTimelineDebug
{
    // Only the map view uses this layer. Battle and title cameras retain their original settings.
    [DefaultExecutionOrder(1000)]
    public sealed class WorldTimelineInkMap : MonoBehaviour
    {
        public const string LayerName = "WorldTimelineMap";
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
            camera.backgroundColor = new Color(.94f, .93f, .90f);
            SetSceneryVisible(true);
            applied = true;
        }
        internal void ExitMap(WorldTimelineBoard requester)
        {
            if (owner == requester) Restore();
        }

        private void Restore()
        {
            SetSceneryVisible(false);
            if (!applied) return;
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
