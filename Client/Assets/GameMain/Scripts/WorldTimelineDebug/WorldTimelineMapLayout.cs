using UnityEngine;

namespace WorldTimelineDebug
{
    // Independent flat-map projection shared by scenery and runtime nodes.
    public sealed class WorldTimelineMapLayout : MonoBehaviour
    {
        public Vector3 Origin;
        public float NodeSpacing = 18;
        [Tooltip("旧样板兼容字段；独立平面地图不读取此字段。")]
        public string SourceTerrainName;
        [Header("Ink tabletop")]
        public bool FlatSurface;
        public bool InkPalette;
        [Header("Map perspective camera")]
        [Range(25, 65)] public float CameraPitch = 48;
        [Range(-180, 180)] public float CameraYaw = -20;
        [Range(25, 75)] public float CameraFieldOfView = 48;
        [Range(.17f, 1)] public float FocusZoom = .31f;

        public Vector3 Project(double q, double r) => Origin + new Vector3(
            1.73205f * ((float)q + (float)r * .5f), 0, -1.5f * (float)r) * NodeSpacing;
    }
}
