using UnityEngine;

namespace WorldTimelineDebug
{
    public sealed class WorldTimelineRegionCulling : MonoBehaviour
    {
        [SerializeField] private WorldTimelineRegion[] regions;
        [SerializeField, Range(.02f, .6f)] private float nearScreenFraction = .18f;
        [SerializeField, Range(.01f, .3f)] private float middleScreenFraction = .10f;
        [SerializeField, Range(0, .3f)] private float hysteresis = .12f;
        [SerializeField, Min(0)] private float viewPadding = 5;
        [SerializeField, Min(0)] private float exitDelay = .35f;
        [SerializeField, Min(.02f)] private float updateInterval = .15f;
        private readonly Plane[] planes = new Plane[6];
        private Camera sceneCamera;
        private float nextUpdate;
        public int NearCount { get; private set; }
        public int MiddleCount { get; private set; }
        public int FarCount { get; private set; }
        public int HiddenCount { get; private set; }
        public int ActiveRendererCount { get; private set; }
        public WorldTimelineRegion[] Regions => regions;
        public void Configure(WorldTimelineRegion[] entries, float padding) { regions = entries; viewPadding = padding; }
        private void LateUpdate()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + updateInterval;
            RefreshVisibility();
        }
        public void RefreshVisibility()
        {
            if (regions == null) return;
            if (sceneCamera == null) sceneCamera = Camera.main;
            bool visible = sceneCamera != null && sceneCamera.isActiveAndEnabled
                && WorldTimelineBoard.ForRenderingCamera(sceneCamera) != null;
            if (visible) GeometryUtility.CalculateFrustumPlanes(sceneCamera, planes);
            NearCount = MiddleCount = FarCount = HiddenCount = ActiveRendererCount = 0;
            foreach (var region in regions)
            {
                if (region == null) continue;
                if (visible) region.Evaluate(sceneCamera, planes, Time.unscaledTime, nearScreenFraction,
                    middleScreenFraction, hysteresis, viewPadding, exitDelay);
                else region.Apply(RegionDetailTier.Hidden);
                switch (region.CurrentTier)
                {
                    case RegionDetailTier.Near: NearCount++; break;
                    case RegionDetailTier.Middle: MiddleCount++; break;
                    case RegionDetailTier.Far: FarCount++; break;
                    default: HiddenCount++; break;
                }
                ActiveRendererCount += region.ActiveRendererCount;
            }
        }
        private void OnDisable()
        {
            if (regions != null) foreach (var region in regions) if (region != null) region.Apply(RegionDetailTier.Near);
        }
    }
}
