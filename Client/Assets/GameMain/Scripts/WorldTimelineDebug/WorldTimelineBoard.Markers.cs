using UnityEngine;
using UnityEngine.Rendering;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineBoard
    {
        private const int TypeMarkerPass = 3;
        private static readonly int BadgeAtlas = Shader.PropertyToID("_TimelineMapBadgeAtlas");
        private static readonly int BadgeUV = Shader.PropertyToID("_TimelineMapBadgeUV");
        private readonly Vector4[] badgeUVs = new Vector4[1023];
        private Mesh badgeQuad;
        internal MapMarkerKind? MarkerFilter { get; set; }

        private bool ShowsTypeBadge(Tile tile) => tile.Kind != MapMarkerKind.Trail &&
            (!MarkerFilter.HasValue || tile.Kind == MarkerFilter);

        private void InitializeTypeMarkers()
        {
            badgeQuad = new Mesh
            {
                name = "轮回关卡标记",
                vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(1, 1), new Vector3(-1, 1) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            meshes.Add(badgeQuad);
        }

        private float TypeMarkerRadius(Camera camera, Vector3 center, Tile tile)
        {
            float depth = Vector3.Dot(center - camera.transform.position, camera.transform.forward);
            float viewportRadius = Mathf.Lerp(.018f, .011f, Mathf.InverseLerp(.17f, MaxMapZoom, zoom));
            float radius = depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) * 2f * viewportRadius;
            return radius * (tile.Kind == MapMarkerKind.Ritual ? 1.25f : 1f);
        }

        private void DrawTypeMarkers(CommandBuffer cmd, Camera camera)
        {
            float now = Time.unscaledTime;
            var rotation = camera.transform.rotation;
            var ringRotation = rotation * Quaternion.Euler(90, 0, 0);
            int count = 0;
            foreach (var pair in tiles)
            {
                var tile = pair.Value;
                var owner = state.Nodes[pair.Key].FirstClear;
                bool showOwner = owner.HasValue && (!MarkerFilter.HasValue || tile.Kind == MarkerFilter);
                if (!showOwner && (!ShowsTypeBadge(tile) || !(tile.Reachable || IsMarkerFocused(pair.Key, tile)))) continue;
                var center = MarkerCenter(tile, tile.LiftAt(now));
                float radius = TypeMarkerRadius(camera, center, tile);
                if (radius <= 0f) continue;
                if (showOwner)
                {
                    Color tint = owner.Value.ActorId == state.PlayerActorId ? new Color(.13f,.54f,.43f) : ActorRouteColor(owner.Value.ActorId,state.ChosenActorId);
                    AddMarker(cmd,pulseMesh,Matrix4x4.TRS(center,ringRotation,Vector3.one*radius*1.43f),tint,ref count);
                    if (!ShowsTypeBadge(tile) || !(tile.Reachable || IsMarkerFocused(pair.Key,tile))) continue;
                }
                Color ring = tile.Selected ? SelectedColor : tile.Reachable ? OpeningDetourColor :
                    new Color(.54f, .46f, .31f);
                AddMarker(cmd, pulseMesh, Matrix4x4.TRS(center, ringRotation, Vector3.one * radius * 1.22f), ring, ref count);
            }
            if (count > 0) DrawMarkerBatch(cmd, pulseMesh, count);

            count = 0;
            var atlas = WorldTimelineMapMarker.Atlas;
            foreach (var pair in tiles)
            {
                var tile = pair.Value;
                if (!ShowsTypeBadge(tile)) continue;
                var center = MarkerCenter(tile, tile.LiftAt(now));
                float radius = TypeMarkerRadius(camera, center, tile);
                if (radius <= 0f) continue;
                var matrix = Matrix4x4.TRS(center, rotation, Vector3.one * radius);
                var rect = WorldTimelineMapMarker.AtlasRect(tile.Kind);
                if (!SystemInfo.supportsInstancing)
                {
                    properties.Clear();
                    properties.SetTexture(BadgeAtlas, atlas);
                    properties.SetVector(BadgeUV, rect);
                    cmd.DrawMesh(badgeQuad, matrix, overlay, 0, TypeMarkerPass, properties);
                    continue;
                }
                markerMatrices[count] = matrix;
                badgeUVs[count++] = rect;
            }
            if (count == 0) return;
            properties.Clear();
            properties.SetTexture(BadgeAtlas, atlas);
            properties.SetVectorArray(BadgeUV, badgeUVs);
            cmd.DrawMeshInstanced(badgeQuad, 0, overlay, TypeMarkerPass, markerMatrices, count, properties);
        }
    }
}
