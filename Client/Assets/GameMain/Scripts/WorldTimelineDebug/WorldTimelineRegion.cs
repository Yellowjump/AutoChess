using UnityEngine;

namespace WorldTimelineDebug
{
    public enum RegionDetailTier { Hidden = -1, Far = 0, Middle = 1, Near = 2 }

    public sealed class WorldTimelineRegion : MonoBehaviour
    {
        [SerializeField] private Vector2Int node;
        [SerializeField] private Bounds localBounds;
        [SerializeField] private GameObject nearDetails;
        [SerializeField] private GameObject middleDetails;
        [SerializeField] private GameObject farLandmark;
        private Renderer[] renderers;
        private float outsideSince = -1;
        public RegionDetailTier CurrentTier { get; private set; } = RegionDetailTier.Near;
        public Vector2Int Node => node;
        public int ActiveRendererCount
        {
            get
            {
                if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
                int count = 0;
                foreach (var renderer in renderers)
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy) count++;
                return count;
            }
        }
        public Bounds WorldBounds
        {
            get
            {
                var matrix = transform.localToWorldMatrix;
                var e = localBounds.extents;
                var x = matrix.MultiplyVector(new Vector3(e.x, 0, 0));
                var y = matrix.MultiplyVector(new Vector3(0, e.y, 0));
                var z = matrix.MultiplyVector(new Vector3(0, 0, e.z));
                var extents = Abs(x) + Abs(y) + Abs(z);
                return new Bounds(matrix.MultiplyPoint3x4(localBounds.center), extents * 2);
            }
        }
        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        public void Configure(Vector2Int coordinate, Bounds bounds, GameObject near, GameObject middle, GameObject far)
        {
            node = coordinate; localBounds = bounds;
            nearDetails = near; middleDetails = middle; farLandmark = far;
            Apply(RegionDetailTier.Near);
        }
        [ContextMenu("Recalculate Region Bounds")]
        public void RecalculateBounds()
        {
            var entries = GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            foreach (var entry in entries)
            {
                var bounds = entry.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = transform.InverseTransformPoint(point);
                    if (!initialized) { localBounds = new Bounds(point, Vector3.zero); initialized = true; }
                    else localBounds.Encapsulate(point);
                }
            }
        }
        public void Evaluate(Camera camera, Plane[] planes, float now, float nearThreshold, float middleThreshold,
            float hysteresis, float padding, float exitDelay)
        {
            var bounds = WorldBounds;
            var padded = bounds;
            padded.Expand(padding * (CurrentTier == RegionDetailTier.Hidden ? 1 : 2));
            if (!GeometryUtility.TestPlanesAABB(planes, padded))
            {
                if (outsideSince < 0) outsideSince = now;
                if (now - outsideSince >= exitDelay) Apply(RegionDetailTier.Hidden);
                return;
            }
            outsideSince = -1;
            var up = Abs(camera.transform.up);
            var right = Abs(camera.transform.right);
            var extents = bounds.extents;
            float halfHeight = camera.orthographic ? camera.orthographicSize
                : Mathf.Max(camera.nearClipPlane, Vector3.Dot(bounds.center - camera.transform.position, camera.transform.forward))
                    * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            float fraction = Mathf.Max(Vector3.Dot(up, extents) / halfHeight,
                Vector3.Dot(right, extents) / (halfHeight * camera.aspect));
            float nearLimit = nearThreshold * (CurrentTier == RegionDetailTier.Near ? 1 - hysteresis : 1 + hysteresis);
            float middleLimit = middleThreshold * (CurrentTier >= RegionDetailTier.Middle ? 1 - hysteresis : 1 + hysteresis);
            Apply(fraction >= nearLimit ? RegionDetailTier.Near
                : fraction >= middleLimit ? RegionDetailTier.Middle : RegionDetailTier.Far);
        }
        public void Apply(RegionDetailTier tier)
        {
            CurrentTier = tier;
            if (nearDetails != null && nearDetails.activeSelf != (tier == RegionDetailTier.Near)) nearDetails.SetActive(tier == RegionDetailTier.Near);
            bool middle = tier == RegionDetailTier.Near || tier == RegionDetailTier.Middle;
            if (middleDetails != null && middleDetails.activeSelf != middle) middleDetails.SetActive(middle);
            if (farLandmark != null && farLandmark.activeSelf != (tier == RegionDetailTier.Far)) farLandmark.SetActive(tier == RegionDetailTier.Far);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.3f, .9f, .8f, .8f);
            var previous = Gizmos.matrix; Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localBounds.center, localBounds.size); Gizmos.matrix = previous;
        }
    }
}
