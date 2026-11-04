using System.Collections.Generic;
using Cinemachine;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed class WorldTimelineBoard : MonoBehaviour
    {
        private static WorldTimelineBoard activeBoard;

        public static bool UsesSceneCamera(Camera camera) => activeBoard != null
            && activeBoard.isActiveAndEnabled && activeBoard.boardCamera == camera;

        public static WorldTimelineBoard ForRenderingCamera(Camera camera) => UsesSceneCamera(camera)
            && activeBoard.cameraBrain != null
            && ReferenceEquals(activeBoard.cameraBrain.ActiveVirtualCamera, activeBoard.mapVirtualCamera)
                ? activeBoard : null;
        private sealed class Tile
        {
            public Vector3 Center;
            public Color Color;
            public bool Important;


        }

        private readonly Dictionary<int, Tile> tiles = new Dictionary<int, Tile>();
        private readonly Dictionary<int, Transform> pieces = new Dictionary<int, Transform>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private MaterialPropertyBlock properties;
        private readonly List<(MeshFilter Filter, Color Color)> pieceParts = new List<(MeshFilter, Color)>();
        private Material overlay;
        private Mesh markerMesh;
        private Mesh links;
        private WorldState state;
        private readonly Matrix4x4[] markerMatrices = new Matrix4x4[1023];
        private readonly Vector4[] markerColors = new Vector4[1023];
        private readonly List<Vector3> linkVertices = new List<Vector3>();
        private readonly List<Color> linkColors = new List<Color>();
        private readonly List<int> linkIndices = new List<int>();
        private static readonly int MarkerColor = Shader.PropertyToID("_MarkerColor");
        private Camera boardCamera;
        private CinemachineVirtualCamera mapVirtualCamera;
        private CinemachineBrain cameraBrain;
        private bool originalOrthographic;
        private Terrain terrain;
        private Collider ground;
        private Vector3 origin;
        private Vector3 focus;
        private float unit;
        private float zoom = 1;
        private float boardRadius;
        private static readonly Color Gold = new Color(1f, .68f, .18f);
        private static readonly Color Blue = new Color(.18f, .67f, 1f);
        private static readonly Color Purple = new Color(.66f, .35f, .94f);

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
        }

        public bool Initialize(WorldState world)
        {
            var layout = FindFirstObjectByType<WorldTimelineMapLayout>();
            var terrainScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("terrain");
            var candidates = terrainScene.isLoaded
                ? terrainScene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Terrain>(true))
                : FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).AsEnumerable();
            terrain = candidates.Where(t => t.terrainData != null)
                .OrderByDescending(t => layout != null && t.name == layout.SourceTerrainName)
                .ThenByDescending(t => t.terrainData.size.x * t.terrainData.size.z)
                .ThenBy(t => t.transform.position.x).ThenBy(t => t.transform.position.z).FirstOrDefault();
            Bounds area;
            if (terrain != null)
            {
                area = terrain.terrainData.bounds;
                area.center += terrain.GetPosition();
            }
            else
            {
                ground = FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Where(c => c.enabled && !c.isTrigger && c.gameObject.scene.name == "terrain")
                    .OrderByDescending(c => c.bounds.size.x * c.bounds.size.z).FirstOrDefault();
                if (ground == null) return false;
                area = ground.bounds;
            }

            overlay = Resources.Load<Material>("WorldTimelineOverlay");
            if (overlay == null) return false;

            links = new Mesh { name = "轮回地图连线" };
            links.MarkDynamic();
            meshes.Add(links);
            var vertices = new Vector3[25];
            var indices = new int[72];
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                indices[i * 3 + 1] = i + 1;
                indices[i * 3 + 2] = (i + 1) % 24 + 1;
            }
            markerMesh = new Mesh { name = "轮回地图点" };
            markerMesh.vertices = vertices;
            markerMesh.triangles = indices;
            meshes.Add(markerMesh);
            origin = area.center;
            boardRadius = world.Nodes.Values.Max(n => Mathf.Max(Mathf.Abs(n.Q), Mathf.Abs(n.R), Mathf.Abs(n.Q + n.R)));
            unit = Mathf.Min(area.size.x, area.size.z) * .68f / (1.73205f * (2 * boardRadius + 1));
            if (layout != null && layout.NodeSpacing > .01f)
            {
                origin = layout.Origin;
                unit = layout.NodeSpacing;
            }
            if (unit <= .01f) return false;
            origin.y = SurfaceHeight(origin);
            focus = origin;
            boardCamera = Camera.main;
            cameraBrain = boardCamera != null ? boardCamera.GetComponent<CinemachineBrain>() : null;
            if (cameraBrain == null)
            {
                Debug.LogError("轮回地图需要场景主相机上的 CinemachineBrain。");
                return false;
            }
            originalOrthographic = boardCamera.orthographic;
            var cameraObject = new GameObject("轮回地图虚拟相机");
            cameraObject.transform.SetParent(transform, false);
            mapVirtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
            mapVirtualCamera.Priority = 100;
            mapVirtualCamera.m_Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            mapVirtualCamera.m_Lens.NearClipPlane = .3f;
            mapVirtualCamera.transform.rotation = Quaternion.Euler(67, 0, 0);
            foreach (var node in world.Nodes.Values) CreateTile(node);
            UpdateCamera();
            activeBoard = this;
            return true;
        }

        private Vector3 Position(double q, double r)
        {
            var position = origin + new Vector3(1.73205f * ((float)q + (float)r * .5f), 0, -1.5f * (float)r) * unit;
            position.y = SurfaceHeight(position);
            return position;
        }

        private float SurfaceHeight(Vector3 position)
        {
            if (terrain != null)
            {
                var local = position - terrain.GetPosition();
                var data = terrain.terrainData;
                return data.GetInterpolatedHeight(local.x / data.size.x, local.z / data.size.z) + terrain.GetPosition().y;
            }
            var bounds = ground.bounds;
            var ray = new Ray(new Vector3(position.x, bounds.max.y + 10, position.z), Vector3.down);
            return ground.Raycast(ray, out var hit, bounds.size.y + 20) ? hit.point.y : bounds.max.y;
        }

        private void CreateTile(NodeState node)
        {
            Vector3 center = Position(node.Q, node.R);
            float lift = Mathf.Max(.08f, unit * .035f);
            tiles.Add(node.Id, new Tile { Center = center + Vector3.up * lift });
        }
        public void Sync(WorldState world, int selectedNode)
        {
            state = world;
            var player = world.Actors[1];
            var groups = world.Actors.Values.Where(actor => actor.IsAlive)
                .Select(actor => ActorMapProjection.Position(world, actor))
                .GroupBy(point => (point.Q, point.R))
                .ToDictionary(group => group.Key, group => group.Count());
            foreach (var node in world.Nodes.Values)
            {
                var tile = tiles[node.Id];
                bool reachable = player.NodeId.HasValue && world.Nodes[player.NodeId.Value].Links.Contains(node.Id);
                var color = new Color(.66f, .79f, .69f);
                if (node.Items.Count > 0) color = new Color(.98f, .73f, .28f);
                if (node.MonsterHealth > 0) color = new Color(.97f, .39f, .31f);
                if (world.Ritual.NodeId == node.Id) color = Purple;
                if (reachable) color = Blue;
                if (selectedNode == node.Id) color = Gold;
                tile.Important = reachable || selectedNode == node.Id || world.Ritual.NodeId == node.Id
                    || node.MonsterHealth > 0 || node.Items.Count > 0 || groups.ContainsKey((node.Q, node.R));
                tile.Color = color;

            }
            RebuildLinks();
            var occupied = new Dictionary<Vector2, int>();
            foreach (var actor in world.Actors.Values.OrderBy(actor => actor.Id))
            {
                if (!actor.IsAlive)
                {
                    if (pieces.TryGetValue(actor.Id, out var dead)) dead.gameObject.SetActive(false);
                    continue;
                }
                if (!pieces.TryGetValue(actor.Id, out var piece))
                {
                    piece = CreatePiece(actor.Id, world.PlayerActorId, world.ChosenActorId);
                    pieces.Add(actor.Id, piece);
                }
                var point = ActorMapProjection.Position(world, actor);
                var key = new Vector2((float)point.Q, (float)point.R);
                occupied.TryGetValue(key, out int slot);
                occupied[key] = slot + 1;
                var position = Position(point.Q, point.R);
                int count = groups[(point.Q, point.R)];
                if (count > 1)
                {
                    float angle = slot * 2 * Mathf.PI / count;
                    float offset = unit * Mathf.Max(.40f, .28f / Mathf.Sin(Mathf.PI / count));
                    position += new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * offset;
                    position.y = SurfaceHeight(position);
                }
                piece.position = position + Vector3.up * Mathf.Max(.1f, unit * .045f);
                piece.gameObject.SetActive(true);
            }
        }

        public void ClearPieces()
        {
            foreach (var piece in pieces.Values)
            {
                piece.gameObject.SetActive(false);
                Destroy(piece.gameObject);
            }
            pieces.Clear();
            pieceParts.Clear();
        }

        private Transform CreatePiece(int id, int playerId, int? chosenId)
        {
            var piece = new GameObject("棋子 #" + id).transform;
            piece.SetParent(transform, false);
            bool special = id == playerId || id == chosenId;
            var color = id == playerId ? Blue : id == chosenId ? Gold : Purple;
            Part(piece, PrimitiveType.Cylinder, new Vector3(0, .07f, 0), new Vector3(.44f, .07f, .44f), color * .72f);
            Part(piece, PrimitiveType.Capsule, new Vector3(0, .30f, 0), new Vector3(.27f, .24f, .27f), color);
            Part(piece, special ? PrimitiveType.Cylinder : PrimitiveType.Sphere, new Vector3(0, .62f, 0),
                special ? new Vector3(.37f, .08f, .37f) : Vector3.one * .25f, color);
            if (special)
                Part(piece, PrimitiveType.Sphere, new Vector3(0, .77f, 0), Vector3.one * .18f, color);
            var label = new GameObject("编号").AddComponent<TextMesh>();
            label.transform.SetParent(piece, false);
            label.transform.localPosition = Vector3.up * unit * .98f;
            label.transform.rotation = boardCamera.transform.rotation;
            label.text = id == playerId ? "P" : id == chosenId ? "K" : "#" + id;
            label.anchor = TextAnchor.MiddleCenter;
            label.characterSize = unit * .19f;
            label.fontSize = 48;
            label.color = Color.white;
            return piece;
        }

        private Transform Part(Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position * unit;
            part.transform.localScale = scale * unit;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            part.GetComponent<Renderer>().enabled = false;
            pieceParts.Add((part.GetComponent<MeshFilter>(), color));
            return part.transform;
        }
        private void RebuildLinks()
        {
            if (state == null) return;
            linkVertices.Clear();
            linkColors.Clear();
            linkIndices.Clear();
            int? playerNode = state.Actors[state.PlayerActorId].NodeId;
            foreach (var node in state.Nodes.Values)
            foreach (int otherId in node.Links)
            {
                if (otherId <= node.Id) continue;
                bool reachable = node.Id == playerNode || otherId == playerNode;
                if (!reachable && zoom >= .65f) continue;
                Vector3 start = tiles[node.Id].Center;
                Vector3 end = tiles[otherId].Center;
                var side = Vector3.Cross(Vector3.up, end - start).normalized * unit * (reachable ? .032f : .016f);
                var color = reachable ? Blue : new Color(.48f, .62f, .62f, .55f);
                int first = linkVertices.Count;
                for (int i = 0; i <= 4; i++)
                {
                    var point = Vector3.Lerp(start, end, i / 4f);
                    point.y = SurfaceHeight(point) + Mathf.Max(.08f, unit * .035f);
                    linkVertices.Add(point - side);
                    linkVertices.Add(point + side);
                    linkColors.Add(color);
                    linkColors.Add(color);
                    if (i == 0) continue;
                    int j = first + (i - 1) * 2;
                    linkIndices.Add(j); linkIndices.Add(j + 1); linkIndices.Add(j + 2);
                    linkIndices.Add(j + 1); linkIndices.Add(j + 3); linkIndices.Add(j + 2);
                }
            }
            links.Clear();
            links.SetVertices(linkVertices);
            links.SetColors(linkColors);
            links.SetTriangles(linkIndices, 0);
        }

        public void DrawOverlay(CommandBuffer cmd, Camera camera)
        {
            if (camera != boardCamera || state == null || cameraBrain == null
                || !ReferenceEquals(cameraBrain.ActiveVirtualCamera, mapVirtualCamera)) return;
            if (linkIndices.Count > 0) cmd.DrawMesh(links, Matrix4x4.identity, overlay, 0, 1);
            DrawMarkers(cmd, true);
            DrawMarkers(cmd, false);
            foreach (var part in pieceParts)
            {
                if (!part.Filter.gameObject.activeInHierarchy) continue;
                properties.Clear();
                properties.SetColor(MarkerColor, part.Color);
                cmd.DrawMesh(part.Filter.sharedMesh, part.Filter.transform.localToWorldMatrix, overlay, 0, 0, properties);
            }
        }

        public int? HoveredNode { get; set; }

        private bool IsVisible(Tile tile) => tile.Important || zoom < .65f;

        private void DrawMarkers(CommandBuffer cmd, bool border)
        {
            int count = 0;
            foreach (var pair in tiles)
            {
                var tile = pair.Value;
                if (!IsVisible(tile)) continue;
                float radius = unit * (border ? .29f : .21f);
                var matrix = Matrix4x4.TRS(tile.Center, boardCamera.transform.rotation * Quaternion.Euler(90, 0, 0),
                    Vector3.one * radius);
                Color color = border ? (HoveredNode == pair.Key ? Color.white : new Color(.04f, .06f, .08f)) : tile.Color;
                if (!SystemInfo.supportsInstancing)
                {
                    properties.Clear();
                    properties.SetColor(MarkerColor, color);
                    cmd.DrawMesh(markerMesh, matrix, overlay, 0, 0, properties);
                    continue;
                }
                markerMatrices[count] = matrix;
                markerColors[count++] = color;
                if (count == markerMatrices.Length)
                {
                    DrawMarkerBatch(cmd, count);
                    count = 0;
                }
            }
            if (count > 0) DrawMarkerBatch(cmd, count);
        }

        private void DrawMarkerBatch(CommandBuffer cmd, int count)
        {
            properties.Clear();
            properties.SetVectorArray(MarkerColor, markerColors);
            cmd.DrawMeshInstanced(markerMesh, 0, overlay, 0, markerMatrices, count, properties);
        }
        public void SetViewport(Rect screenRect)
        {
            // The scene camera keeps its existing full-screen viewport and UI stack.
            UpdateCamera();
        }

        public void Focus(double q, double r) { focus = Position(q, r); zoom = .45f; RebuildLinks(); UpdateCamera(); }
        public void FocusActor(int id)
        {
            if (!pieces.TryGetValue(id, out var piece) || !piece.gameObject.activeInHierarchy) return;
            focus = piece.position;
            zoom = .45f;
            RebuildLinks();
            UpdateCamera();
        }

        public void Overview() { focus = origin; zoom = 1; RebuildLinks(); UpdateCamera(); }
        public void Zoom(float factor) { bool detailed = zoom < .65f; zoom = Mathf.Clamp(zoom * factor, .17f, 1.5f); if (detailed != (zoom < .65f)) RebuildLinks(); UpdateCamera(); }

        public void Pan(Vector2 pixels)
        {
            float scale = boardCamera.orthographicSize * 2 / Mathf.Max(1, boardCamera.pixelHeight);
            focus += new Vector3(-pixels.x, 0, pixels.y / .92f) * scale;
            focus.x = Mathf.Clamp(focus.x, origin.x - unit * boardRadius * 2, origin.x + unit * boardRadius * 2);
            focus.z = Mathf.Clamp(focus.z, origin.z - unit * boardRadius * 2, origin.z + unit * boardRadius * 2);
            focus.y = SurfaceHeight(focus);
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            mapVirtualCamera.m_Lens.OrthographicSize = unit * (boardRadius + 1) * Mathf.Max(1.65f, 1.95f / boardCamera.aspect) * zoom;

            float distance = unit * (boardRadius + 1) * 3;
            mapVirtualCamera.m_Lens.FarClipPlane = distance * 2 + 100;
            mapVirtualCamera.transform.position = focus - mapVirtualCamera.transform.forward * distance;
        }

        public int? Pick(Vector2 screenPosition)
        {
            int? selected = null;
            float radius = unit * .29f * boardCamera.pixelHeight / (2 * boardCamera.orthographicSize);
            float distance = Mathf.Pow(radius + 5, 2);
            foreach (var pair in tiles)
            {
                if (!IsVisible(pair.Value)) continue;
                var center = boardCamera.WorldToScreenPoint(pair.Value.Center);
                if (center.z < boardCamera.nearClipPlane || center.z > boardCamera.farClipPlane) continue;
                float candidate = (screenPosition - (Vector2)center).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate;
                selected = pair.Key;
            }
            return selected;
        }
        private void OnDestroy()
        {
            if (activeBoard == this) activeBoard = null;
            if (mapVirtualCamera != null) mapVirtualCamera.gameObject.SetActive(false);
            if (mapVirtualCamera != null && boardCamera != null) boardCamera.orthographic = originalOrthographic;
            foreach (var mesh in meshes) Destroy(mesh);

        }
    }
}
