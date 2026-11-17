using System.Collections.Generic;
using Cinemachine;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineBoard : MonoBehaviour
    {
        private static WorldTimelineBoard activeBoard;

        public static bool UsesSceneCamera(Camera camera) => activeBoard != null
            && activeBoard.isActiveAndEnabled && activeBoard.boardCamera == camera;

        // Presentation state is owned by map/battle lifecycle, never by a transient Brain render query.
        public static WorldTimelineBoard ForRenderingCamera(Camera camera) => UsesSceneCamera(camera)
            && activeBoard.mapVirtualCamera != null && activeBoard.presentationSuspensions == 0 ? activeBoard : null;

        private int presentationSuspensions;
        public static System.IDisposable SuspendMapPresentation()
        {
            var board = activeBoard;
            if (board == null) return null;
            board.presentationSuspensions++;
            if (board.presentationSuspensions == 1) board.inkMap?.ExitMap(board);
            return new PresentationScope(board);
        }
        private sealed class PresentationScope : System.IDisposable
        {
            private WorldTimelineBoard board;
            public PresentationScope(WorldTimelineBoard value) { board = value; }
            public void Dispose()
            {
                var previous = board;
                board = null;
                if (previous == null) return;
                previous.presentationSuspensions--;
                if (previous.presentationSuspensions == 0 && previous == activeBoard && previous.isActiveAndEnabled)
                    previous.inkMap?.EnterMap(previous, previous.boardCamera);
            }
        }
        private sealed class Tile
        {
            public Vector3 Center;
            public Color Color;
            public bool ImportantWithoutSelection;
            public bool HighlightedWithoutSelection;
            public bool Important => Selected || ImportantWithoutSelection;
            public bool Highlighted => Selected || HighlightedWithoutSelection;
            public MapMarkerKind Kind;
            public bool Reachable;
            public bool Selected;
            public bool Visited;
            public float LiftFrom;
            public float LiftStarted;

            public float LiftAt(float now) => Mathf.SmoothStep(LiftFrom, Reachable ? 1f : 0f,
                (now - LiftStarted) / .30f);
        }

        private readonly Dictionary<int, Tile> tiles = new Dictionary<int, Tile>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private MaterialPropertyBlock properties;
        private Material overlay;
        private Mesh markerMesh;
        private Mesh sealMesh;
        private Mesh pulseMesh;
        private Mesh links;
        private Mesh selectionLinks;
        private int? selectedTileId;
        private WorldState state;
        private readonly Matrix4x4[] markerMatrices = new Matrix4x4[1023];
        private readonly Vector4[] markerColors = new Vector4[1023];
        private readonly List<Vector3> linkVertices = new List<Vector3>();
        private readonly List<Color> linkColors = new List<Color>();
        private readonly List<int> linkIndices = new List<int>();
        private readonly Dictionary<(int, int), int> openingLinks = new Dictionary<(int, int), int>();
        private readonly Dictionary<int, int> openingStops = new Dictionary<int, int>();
        private int shortcutEntry, detourEntry, openingPreview, openingCommitted;
        private int? openingNextStop;
        private Transform openingGateMarker;
        private Transform openingSwordMarker;
        private Mesh landmarkQuad, swordMesh;
        private Vector3 swordFlightFrom, swordRestOffset;
        private float swordFlightStarted = -1f;
        private bool swordPlaced;
        private int openingGateId, openingSwordId;
        public static readonly Color OpeningShortcutColor = new Color(.65f, .26f, .16f);
        public static readonly Color OpeningDetourColor = new Color(.24f, .43f, .38f);
        private static readonly int MarkerColor = Shader.PropertyToID("_MarkerColor");
        private Camera boardCamera;
        private CinemachineVirtualCamera mapVirtualCamera;
        private CinemachineBrain cameraBrain;
        private bool originalOrthographic;
        private Vector3 origin;
        private Vector3 focus;
        private float unit;
        private const float MaxMapZoom = .78f;
        public static float WheelZoomFactor(float steps) => Mathf.Pow(1.1f, steps * 6f);
        private float zoom = MaxMapZoom;
        private float boardRadius;
        private WorldTimelineMapLayout mapLayout;
        private WorldTimelineInkMap inkMap;
        public WorldTimelineInkMap InkMap => inkMap;
        internal Vector4 WeatherField => new Vector4(origin.x, origin.z, 1f / unit, 1f);
        internal float PlayerDusk { get; private set; }
        private bool Ink => mapLayout != null && mapLayout.InkPalette;
        public int OverlayDrawCount { get; private set; }
        private Color PlayerColor => Ink ? new Color(.29f, .43f, .38f) : Blue;
        private Color SelectedColor => Ink ? new Color(.57f, .48f, .31f) : Gold;
        private static readonly Color Gold = new Color(1f, .68f, .18f);
        private static readonly Color Blue = new Color(.18f, .67f, 1f);
        private static readonly Color Purple = new Color(.66f, .35f, .94f);

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
        }

        public bool Initialize(WorldState world)
        {
            var layout = FindObjectsByType<WorldTimelineMapLayout>(FindObjectsSortMode.None)
                .OrderByDescending(entry => entry.FlatSurface).FirstOrDefault();
            mapLayout = layout;
            inkMap = layout != null ? layout.GetComponent<WorldTimelineInkMap>() : null;
            if (layout == null || layout.NodeSpacing <= .01f)
            {
                Debug.LogError("轮回地图缺少有效的 WorldTimelineMapLayout，请配置原点和节点间距。");
                return false;
            }

            overlay = Resources.Load<Material>("WorldTimelineOverlay");
            if (overlay == null) return false;

            links = new Mesh { name = "轮回地图连线" };
            selectionLinks = new Mesh { name = "轮回地图选中连线" };
            links.MarkDynamic();
            meshes.Add(links);
            meshes.Add(selectionLinks);
            actorRouteMesh = new Mesh { name = "轮回角色路线", indexFormat = IndexFormat.UInt32 };
            actorRouteMesh.MarkDynamic();
            meshes.Add(actorRouteMesh);
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
            sealMesh = new Mesh
            {
                name = "轮回地图菱形路标",
                vertices = new[] { Vector3.left, Vector3.forward, Vector3.right, Vector3.back },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            meshes.Add(sealMesh);
            pulseMesh = CreatePulseMesh();
            meshes.Add(pulseMesh);
            InitializeTypeMarkers();
            origin = layout.Origin;
            boardRadius = world.Nodes.Values.Max(n => Mathf.Max(Mathf.Abs(n.Q), Mathf.Abs(n.R), Mathf.Abs(n.Q + n.R)));
            UpdatePlayerWeather(world);
            unit = layout.NodeSpacing;
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
            // Cinemachine 2.x filters virtual cameras using the output camera's culling mask.
            // The virtual camera must remain eligible after EnterMap isolates the map layer.
            int cameraLayer = LayerMask.NameToLayer(WorldTimelineInkMap.LayerName);
            if (inkMap != null && cameraLayer >= 0) cameraObject.layer = cameraLayer;
            mapVirtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
            mapVirtualCamera.Priority = 100;
            mapVirtualCamera.m_Lens.ModeOverride = LensSettings.OverrideModes.Perspective;
            mapVirtualCamera.m_Lens.FieldOfView = layout != null ? Mathf.Clamp(layout.CameraFieldOfView, 25, 75) : 48;
            mapVirtualCamera.m_Lens.NearClipPlane = .3f;
            mapVirtualCamera.transform.rotation = Quaternion.Euler(
                layout != null ? Mathf.Clamp(layout.CameraPitch, 25, 65) : 48,
                layout != null ? layout.CameraYaw : -20, 0);
            foreach (var node in world.Nodes.Values) CreateTile(node);
            UpdateCamera();
            if (activeBoard != null && activeBoard != this) activeBoard.inkMap?.ExitMap(activeBoard);
            activeBoard = this;
            inkMap?.EnterMap(this, boardCamera);
            return true;
        }

        private Vector3 Position(double q, double r)
        {
            return mapLayout.Project(q, r);
        }

        private void CreateTile(NodeState node)
        {
            Vector3 center = Position(node.Q, node.R);
            float lift = Mathf.Max(.08f, unit * .035f);
            tiles.Add(node.Id, new Tile { Center = center + Vector3.up * lift });
        }

        private static Mesh CreatePulseMesh()
        {
            const int sides = 24;
            var vertices = new Vector3[sides * 2];
            var indices = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * 2f * Mathf.PI / sides;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                int outer = i * 2;
                int next = ((i + 1) % sides) * 2;
                vertices[outer] = direction;
                vertices[outer + 1] = direction * .88f;
                int index = i * 6;
                indices[index] = outer;
                indices[index + 1] = next;
                indices[index + 2] = outer + 1;
                indices[index + 3] = next;
                indices[index + 4] = next + 1;
                indices[index + 5] = outer + 1;
            }
            return new Mesh { name = "可达点光圈", vertices = vertices, triangles = indices };
        }

        public void Sync(WorldSimulation simulation, int selectedNode, float travelSeconds = .38f)
        {
            var world = simulation.State;
            routeSimulation = simulation;
            state = world;
            UpdatePlayerWeather(world);
            var player = world.Actors[world.PlayerActorId];
            if (player.NodeId.HasValue) tiles[player.NodeId.Value].Visited = true;
            int? playerNode = player.IsAlive && !world.PlayerControlEnded && !world.IsEnded
                && !player.LeaderId.HasValue && player.CurrentAction == null ? player.NodeId : null;
            float now = Time.unscaledTime;
            var groups = world.Actors.Values.Where(actor => actor.IsAlive)
                .Select(actor => ActorMapProjection.Position(world, actor))
                .GroupBy(point => (point.Q, point.R))
                .ToDictionary(group => group.Key, group => group.Count());
            var view = GetComponentInParent<WorldTimelineDebugView>();
            foreach (var node in world.Nodes.Values)
            {
                var tile = tiles[node.Id];
                bool reachable = playerNode.HasValue && world.Nodes[playerNode.Value].Links.Contains(node.Id);
                if (tile.Reachable != reachable)
                {
                    tile.LiftFrom = tile.LiftAt(now);
                    tile.LiftStarted = now;
                    tile.Reachable = reachable;
                }
                bool hasStory = simulation.Stories != null &&
                    simulation.Stories.TryGetDefinition(node.Id, out var story) && !story.IsMerchant &&
                    WorldTimelineDebugView.CanSeeStory(story, player);
                tile.Kind = WorldTimelineMapMarker.Classify(node, hasStory, world.Ritual?.NodeId == node.Id,
                    view?.HasClearedGuardian(node) ?? node.GuardianDefeat.HasValue);
                var color = new Color(.66f, .79f, .69f);
                if (node.IsCultivationSite) color = new Color(.32f, .58f, .51f);
                if (hasStory) color = new Color(.72f, .53f, .32f);
                if (node.IsMerchant) color = new Color(.81f, .60f, .26f);
                if (node.Items.Count > 0) color = new Color(.98f, .73f, .28f);
                if (node.MonsterHealth > 0) color = new Color(.97f, .39f, .31f);
                if (world.Ritual.NodeId == node.Id) color = Purple;
                if (reachable) color = Blue;
                if (Ink)
                {
                    color = node.MonsterHealth > 0 ? new Color(.31f, .32f, .30f) : new Color(.80f, .79f, .75f);
                    if (node.IsCultivationSite) color = new Color(.48f, .59f, .53f);
                    if (tile.Kind == MapMarkerKind.Story) color = new Color(.65f, .48f, .30f);
                    if (node.IsMerchant) color = new Color(.69f, .51f, .27f);
                    if (node.Items.Count > 0) color = new Color(.61f, .59f, .53f);
                    if (reachable) color = PlayerColor;
                }
                tile.ImportantWithoutSelection = reachable || tile.Kind != MapMarkerKind.Trail || groups.ContainsKey((node.Q, node.R));
                tile.HighlightedWithoutSelection = reachable || world.Ritual.NodeId == node.Id
                    || groups.ContainsKey((node.Q, node.R));
                tile.Color = color;

            }
            SetSelectedTile(selectedNode);
            RebuildLinks();
            RebuildActorRoutes();
            var occupied = new Dictionary<Vector2, int>();
            foreach (var actor in world.Actors.Values.OrderBy(actor => actor.Id))
            {
                if (!actor.IsAlive)
                {
                    if (pieces.TryGetValue(actor.Id, out var dead)) dead.Root.gameObject.SetActive(false);
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
                    position.y = origin.y;
                }
                piece.MoveTo(position + Vector3.up * Mathf.Max(.1f, unit * .045f), now, travelSeconds);
            }
            if (openingSwordMarker != null) SyncOpeningSword(world);
        }

        private void SyncOpeningSword(WorldState world)
        {
            var holder = world.Actors.Values.FirstOrDefault(actor => actor.IsAlive && actor.Items.Contains(openingSwordId));
            var anchor = holder != null && pieces.TryGetValue(holder.Id, out var owner) ? owner.Root : openingGateMarker;
            if (swordPlaced && openingSwordMarker.parent == openingGateMarker && anchor != openingGateMarker)
            {
                swordFlightFrom = openingSwordMarker.position;
                swordFlightStarted = Time.unscaledTime;
            }
            if (openingSwordMarker.parent != anchor) openingSwordMarker.SetParent(anchor, true);
            swordRestOffset = anchor == openingGateMarker ? Vector3.up * unit * 1.64f :
                (Vector3.up * 1.24f + boardCamera.transform.right * .44f) * unit;
            if (swordFlightStarted < 0f) openingSwordMarker.localPosition = swordRestOffset;
            swordPlaced = true;
            openingSwordMarker.gameObject.SetActive(holder != null || world.Nodes[openingGateId].Items.Contains(openingSwordId));
        }

        public void ClearPieces()
        {
            focusedRouteActor = null;
            routeSimulation = null;
            actorRouteIndices.Clear();
            actorRouteMesh.Clear();
            SetOpeningPaths(0, null, null);
            foreach (var tile in tiles.Values) tile.Visited = false;
            foreach (var piece in pieces.Values)
            {
                piece.Root.gameObject.SetActive(false);
                Destroy(piece.Root.gameObject);
            }
            pieces.Clear();
        }

        public void SetOpeningPaths(int start, IReadOnlyList<int> shortcut, IReadOnlyList<int> detour, int swordId = 0)
        {
            openingLinks.Clear();
            openingStops.Clear();
            openingPreview = openingCommitted = 0;
            openingNextStop = null;
            swordFlightStarted = -1f;
            swordPlaced = false;
            if (openingSwordMarker != null)
            {
                openingSwordMarker.gameObject.SetActive(false);
                Destroy(openingSwordMarker.gameObject);
                openingSwordMarker = null;
            }
            if (openingGateMarker != null)
            {
                openingGateMarker.gameObject.SetActive(false);
                Destroy(openingGateMarker.gameObject);
                openingGateMarker = null;
            }
            if (shortcut == null || detour == null)
            {
                RebuildLinks();
                return;
            }
            shortcutEntry = shortcut[0];
            detourEntry = detour[0];
            openingGateId = shortcut[shortcut.Count - 1];
            AddPath(shortcut, 1);
            AddPath(detour, 2);
            openingSwordId = swordId;
            var gate = tiles[openingGateId];
            openingGateMarker = new GameObject("古修石门").transform;
            openingGateMarker.SetParent(transform, false);
            openingGateMarker.position = gate.Center - Vector3.up * Mathf.Max(.08f, unit * .035f);
            EnsureOpeningMeshes();
            openingSwordMarker = new GameObject("石门机缘").transform;
            openingSwordMarker.SetParent(openingGateMarker, false);

            void AddPath(IReadOnlyList<int> path, int route)
            {
                int previous = start;
                foreach (int next in path)
                {
                    openingLinks[(Mathf.Min(previous, next), Mathf.Max(previous, next))] = route;
                    if (next != openingGateId) openingStops[next] = route;
                    previous = next;
                }
            }
        }

        private void EnsureOpeningMeshes()
        {
            if (landmarkQuad != null) return;
            landmarkQuad = new Mesh
            {
                name = "古修石门接触阴影",
                vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
                triangles = new[] { 0, 2, 1, 1, 2, 3 }
            };
            swordMesh = new Mesh
            {
                name = "古修残剑剪影",
                vertices = new[]
                {
                    new Vector3(-.06f, .04f), new Vector3(.06f, .04f), new Vector3(.06f, -.12f),
                    new Vector3(-.04f, -.19f), new Vector3(-.06f, -.16f),
                    new Vector3(-.15f, .04f), new Vector3(.15f, .04f), new Vector3(.12f, .09f), new Vector3(-.12f, .09f),
                    new Vector3(-.035f, .09f), new Vector3(.035f, .09f), new Vector3(.03f, .19f), new Vector3(-.03f, .19f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 5, 6, 7, 5, 7, 8, 9, 10, 11, 9, 11, 12 }
            };
            meshes.Add(landmarkQuad);
            meshes.Add(swordMesh);
        }

        public void RestoreVisited(IEnumerable<WorldEvent> events, int playerId)
        {
            foreach (var entry in events)
                if (entry.ActorId == playerId && entry.Kind == WorldEventKind.Arrived && entry.NodeId.HasValue &&
                    tiles.TryGetValue(entry.NodeId.Value, out var tile)) tile.Visited = true;
        }

        public int OpeningRouteAt(Vector2 screen)
        {
            if (openingLinks.Count == 0 || openingCommitted != 0) return 0;
            return Pick(screen) is int node && openingStops.TryGetValue(node, out int route) ? route : 0;
        }

        public void SetOpeningRouteFocus(int route, bool committed = false)
        {
            if (openingPreview == route && (!committed || openingCommitted == route)) return;
            if (committed) openingCommitted = route;
            openingPreview = route;
            RebuildLinks();
        }

        public void SetOpeningNextStop(int? node) => openingNextStop = node;

        private bool IsOpeningEntry(int node) => node == shortcutEntry || node == detourEntry;
        private Color RouteColor(int route)
        {
            var color = route == 1 ? OpeningShortcutColor : OpeningDetourColor;
            int focusRoute = openingCommitted != 0 ? openingCommitted : openingPreview;
            color.a = focusRoute == 0 || route == focusRoute ? .92f : .23f;
            return color;
        }

        public void SelectNode(int nodeId)
        {
            if (selectedTileId == nodeId) return;
            SetSelectedTile(nodeId);
            RebuildSelectionLinks();
        }

        private void SetSelectedTile(int nodeId)
        {
            if (selectedTileId.HasValue) tiles[selectedTileId.Value].Selected = false;
            selectedTileId = nodeId;
            tiles[nodeId].Selected = true;
        }

        private void RebuildLinks()
        {
            if (state == null) return;
            int? playerNode = state.Actors[state.PlayerActorId].NodeId;
            foreach (var node in state.Nodes.Values)
            foreach (int otherId in node.Links)
            {
                if (otherId <= node.Id) continue;
                bool reachable = node.Id == playerNode || otherId == playerNode;
                bool openingLink = openingLinks.TryGetValue((node.Id, otherId), out int route);
                if (!openingLink && !reachable && (Ink || zoom >= .65f)) continue;
                AddLink(node.Id, otherId, reachable, openingLink, route);
            }
            UploadLinks(links);
            RebuildSelectionLinks();
        }

        private void RebuildSelectionLinks()
        {
            if (!Ink || state == null || !selectedTileId.HasValue) return;
            int? playerNode = state.Actors[state.PlayerActorId].NodeId;
            int nodeId = selectedTileId.Value;
            foreach (int otherId in state.Nodes[nodeId].Links)
            {
                if (nodeId == playerNode || otherId == playerNode) continue;
                int from = Mathf.Min(nodeId, otherId);
                int to = Mathf.Max(nodeId, otherId);
                if (!openingLinks.ContainsKey((from, to)))
                    AddLink(from, to, false, false, 0);
            }
            UploadLinks(selectionLinks);
        }

        private void AddLink(int from, int to, bool reachable, bool openingLink, int route)
        {
            Vector3 start = tiles[from].Center;
            Vector3 end = tiles[to].Center;
            float width = openingLink ? .027f : Ink ? .014f : reachable ? .032f : .016f;
            var side = Vector3.Cross(Vector3.up, end - start).normalized * unit;
            var color = openingLink ? RouteColor(route) :
                Ink ? new Color(.27f, .35f, .32f, openingLinks.Count > 0 ? .30f : .48f) :
                reachable ? PlayerColor : new Color(.48f, .62f, .62f, .55f);
            var edge = new Color(color.r, color.g, color.b, Ink ? 0f : color.a);
            int first = linkVertices.Count;
            const int segments = 8;
            for (int i = 0; i <= segments; i++)
            {
                float progress = (float)i / segments;
                float curve = openingLink ? Mathf.Sin(progress * Mathf.PI) * ((from & 1) == 0 ? .044f : -.044f) : 0f;
                var point = Vector3.Lerp(start, end, progress) + side * curve;
                float brushWidth = width * (openingLink ? 1f + .15f * Mathf.Sin(i * 1.9f + to) : 1f);
                linkVertices.Add(point - side * brushWidth);
                linkVertices.Add(point);
                linkVertices.Add(point + side * brushWidth);
                linkColors.Add(edge);
                linkColors.Add(color);
                linkColors.Add(edge);
                if (i == 0) continue;
                int j = first + (i - 1) * 3;
                linkIndices.Add(j); linkIndices.Add(j + 1); linkIndices.Add(j + 3);
                linkIndices.Add(j + 1); linkIndices.Add(j + 4); linkIndices.Add(j + 3);
                linkIndices.Add(j + 1); linkIndices.Add(j + 2); linkIndices.Add(j + 4);
                linkIndices.Add(j + 2); linkIndices.Add(j + 5); linkIndices.Add(j + 4);
            }
        }

        private void UploadLinks(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(linkVertices);
            mesh.SetColors(linkColors);
            mesh.SetTriangles(linkIndices, 0);
            linkVertices.Clear();
            linkColors.Clear();
            linkIndices.Clear();
        }

        public void DrawOverlay(CommandBuffer cmd, Camera camera)
        {
            if (state == null || ForRenderingCamera(camera) != this) return;
            OverlayDrawCount++;
            bool showGate = openingGateMarker != null && inkMap != null && inkMap.LandmarkMaterial != null;
            if (showGate && inkMap != null && inkMap.ContactShadow != null)
            {
                var shadow = Matrix4x4.TRS(openingGateMarker.position + Vector3.up * .07f, Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(2.65f, 1.58f, 1f) * unit);
                cmd.DrawMesh(landmarkQuad, shadow, inkMap.ContactShadow, 0, 0);
            }
            if (links.vertexCount > 0) cmd.DrawMesh(links, Matrix4x4.identity, overlay, 0, 1);
            if (Ink && selectionLinks.vertexCount > 0)
                cmd.DrawMesh(selectionLinks, Matrix4x4.identity, overlay, 0, 1);
            DrawActorRoutes(cmd);
            if (showGate)
            {
                var anchor = openingGateMarker.position;
                var matrix = Matrix4x4.TRS(anchor, Quaternion.Euler(0, 337, 0), Vector3.one * unit);
                cmd.DrawMesh(inkMap.GateMesh, matrix, inkMap.LandmarkMaterial, 0, 0);
            }
            if (Ink)
            {
                DrawInkMarkers(cmd, false, false);
                DrawInkMarkers(cmd, true, true);
                DrawInkMarkers(cmd, true, false);
            }
            else
            {
                DrawReachablePulses(cmd);
                DrawMarkers(cmd, true);
                DrawMarkers(cmd, false);
            }
            if (openingLinks.Count == 0) DrawTypeMarkers(cmd, camera);
            DrawPieces(cmd, camera);
            if (openingSwordMarker != null && openingSwordMarker.gameObject.activeInHierarchy) DrawOpeningSword(cmd, camera);
        }

        private void DrawOpeningSword(CommandBuffer cmd, Camera camera)
        {
            float progress = swordFlightStarted < 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - swordFlightStarted) / .58f);
            var destination = openingSwordMarker.parent.position + swordRestOffset;
            var position = Vector3.Lerp(swordFlightFrom, destination, Mathf.SmoothStep(0f, 1f, progress));
            position += Vector3.up * (.37f * unit * Mathf.Sin(progress * Mathf.PI));
            openingSwordMarker.position = position;
            var rotation = camera.transform.rotation * Quaternion.Euler(0, 0, -22);
            float scale = unit * (1f + .55f * Mathf.Sin(progress * Mathf.PI));
            properties.Clear();
            properties.SetColor(MarkerColor, new Color(.32f, .16f, .048f));
            cmd.DrawMesh(swordMesh, Matrix4x4.TRS(position, rotation, Vector3.one * (scale * 1.24f)), overlay, 0, 0, properties);
            properties.SetColor(MarkerColor, new Color(1f, .79f, .36f));
            cmd.DrawMesh(swordMesh, Matrix4x4.TRS(position, rotation, Vector3.one * scale), overlay, 0, 0, properties);
        }
        public int? HoveredNode { get; set; }

        private bool IsVisible(Tile tile) => Ink || tile.Important || zoom < .65f;

        private Vector3 MarkerCenter(Tile tile, float lift) => tile.Center + Vector3.up * (unit * (Ink ? .10f : .22f) * lift);

        private float InkLift(Tile tile, float now) => openingLinks.Count == 0 ? tile.LiftAt(now) : 0f;

        private float InkRadius(int node, Tile tile, float lift)
        {
            if (openingLinks.Count > 0)
            {
                if (node == openingNextStop || node == openingGateId || IsOpeningEntry(node) && openingCommitted == 0)
                    return .118f;
                if (openingStops.ContainsKey(node)) return .091f;
            }
            if (tile.Selected) return .110f;
            return Mathf.Lerp(tile.Important ? .065f : .048f, .110f, lift);
        }

        private void DrawInkMarkers(CommandBuffer cmd, bool seals, bool border)
        {
            int count = 0;
            float dusk = inkMap != null ? inkMap.Dusk : 0f;
            bool opening = openingLinks.Count > 0;
            float now = Time.unscaledTime;
            var mesh = seals ? sealMesh : markerMesh;
            var rotation = boardCamera.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
            foreach (var pair in tiles)
            {
                int node = pair.Key;
                var tile = pair.Value;
                if (!opening && tile.Kind != MapMarkerKind.Trail) continue;
                bool onRoute = openingStops.TryGetValue(node, out int route);
                bool gate = opening && node == openingGateId;
                bool useSeal = onRoute || gate || tile.Reachable || tile.Selected;
                if (useSeal != seals) continue;
                float lift = InkLift(tile, now);
                float radius = InkRadius(node, tile, lift);
                bool focused = tile.Selected || HoveredNode == node ||
                    opening && onRoute && IsOpeningEntry(node) && openingPreview == route;
                if (focused && seals) radius *= 1.22f;
                var color = gate || tile.Selected && !onRoute ? SelectedColor :
                    onRoute ? RouteColor(route) :
                    tile.Reachable ? OpeningDetourColor :
                    tile.Visited ? new Color(.25f, .25f, .22f, .88f) :
                    tile.Kind == MapMarkerKind.Story ? new Color(.65f, .42f, .23f, .92f) :
                    new Color(.34f, .32f, .27f, tile.Important ? .84f : .63f);
                if (onRoute) color.a = Mathf.Max(.63f, color.a);
                if (!border && seals)
                {
                    radius *= .61f;
                    if (node == openingNextStop || (onRoute && node == shortcutEntry) || focused)
                        color = Color.Lerp(color, new Color(.98f, .90f, .71f, color.a), .32f);
                    else color = new Color(.88f, .85f, .73f, color.a);
                }
                if (dusk > 0f) color = Color.Lerp(color, new Color(.80f, .86f, .88f, color.a), .50f * dusk);
                var matrix = Matrix4x4.TRS(MarkerCenter(tile, lift), rotation, Vector3.one * (radius * unit));
                AddMarker(cmd, mesh, matrix, color, ref count);
            }
            if (count > 0) DrawMarkerBatch(cmd, mesh, count);
        }

        private float PulsePhase(int nodeId, Tile tile)
            => Mathf.Repeat((Time.unscaledTime - tile.LiftStarted) / 2.5f + (nodeId & 7) * .06f, 1f);

        private bool IsMarkerFocused(int nodeId, Tile tile) => tile.Selected || HoveredNode == nodeId;

        private float MarkerRadius(int nodeId, Tile tile, bool border, float lift, bool picking = false)
        {
            float ordinary = Ink ? (border ? .12f : .075f) : (border ? .29f : .21f);
            float highlighted = Ink ? (border ? .19f : .12f) : ordinary;
            float resting = tile.Highlighted && !tile.Reachable ? highlighted : ordinary;
            float breath = 0f;
            if (tile.Reachable)
            {
                // Keep the hit area stable while breathing and when hover stops the animation.
                if (picking) breath = .10f;
                else if (!IsMarkerFocused(nodeId, tile))
                    breath = .10f * (.5f - .5f * Mathf.Cos(PulsePhase(nodeId, tile) * 2f * Mathf.PI));
            }
            return unit * Mathf.Lerp(resting, highlighted * 1.4f * (1f + breath), lift);
        }

        private void DrawReachablePulses(CommandBuffer cmd)
        {
            int count = 0;
            float now = Time.unscaledTime;
            foreach (var pair in tiles)
            {
                var tile = pair.Value;
                float lift = tile.LiftAt(now);
                if (lift <= 0f) continue;
                bool resting = IsMarkerFocused(pair.Key, tile);
                float phase = resting ? 0f : PulsePhase(pair.Key, tile);
                float radius = unit * Mathf.Lerp(.24f, .46f, phase);
                var color = tile.Selected ? SelectedColor : Ink ? new Color(.38f, .67f, .56f) : Blue;
                color.a = lift * (resting ? .60f : .65f * (1f - phase));
                var matrix = Matrix4x4.TRS(tile.Center, Quaternion.identity, Vector3.one * radius);
                AddMarker(cmd, pulseMesh, matrix, color, ref count);
            }
            if (count > 0) DrawMarkerBatch(cmd, pulseMesh, count);
        }

        private void DrawMarkers(CommandBuffer cmd, bool border)
        {
            int count = 0;
            float dusk = Ink && inkMap != null ? inkMap.Dusk : 0f;
            foreach (var pair in tiles)
            {
                var tile = pair.Value;
                if (!IsVisible(tile) || openingLinks.Count == 0 && tile.Kind != MapMarkerKind.Trail) continue;
                float lift = tile.LiftAt(Time.unscaledTime);
                float radius = MarkerRadius(pair.Key, tile, border, lift);
                var matrix = Matrix4x4.TRS(MarkerCenter(tile, lift), boardCamera.transform.rotation * Quaternion.Euler(90, 0, 0),
                    Vector3.one * radius);
                Color color = border ? (HoveredNode == pair.Key ? Color.white : new Color(.04f, .06f, .08f)) : tile.Selected ? SelectedColor : tile.Color;
                if (Ink && border && !tile.Highlighted && HoveredNode != pair.Key)
                    color = Color.Lerp(new Color(.47f, .48f, .46f), new Color(.15f, .18f, .23f), dusk);
                if (!border && dusk > 0f)
                    color = Color.Lerp(color, new Color(.79f, .85f, .88f), .48f * dusk);
                if (border && tile.Selected && tile.Reachable) color = Gold;
                AddMarker(cmd, markerMesh, matrix, color, ref count);
            }
            if (count > 0) DrawMarkerBatch(cmd, markerMesh, count);
        }

        private void AddMarker(CommandBuffer cmd, Mesh mesh, Matrix4x4 matrix, Color color, ref int count)
        {
            if (!SystemInfo.supportsInstancing)
            {
                properties.Clear();
                properties.SetColor(MarkerColor, color);
                cmd.DrawMesh(mesh, matrix, overlay, 0, 0, properties);
                return;
            }
            markerMatrices[count] = matrix;
            markerColors[count++] = color;
            if (count < markerMatrices.Length) return;
            DrawMarkerBatch(cmd, mesh, count);
            count = 0;
        }

        private void DrawMarkerBatch(CommandBuffer cmd, Mesh mesh, int count)
        {
            properties.Clear();
            properties.SetVectorArray(MarkerColor, markerColors);
            cmd.DrawMeshInstanced(mesh, 0, overlay, 0, markerMatrices, count, properties);
        }

        private void UpdatePlayerWeather(WorldState world)
        {
            // Retain the last observed weather after the player dies.
            if (!world.Actors.TryGetValue(world.PlayerActorId, out var player) || !player.IsAlive) return;
            var point = ActorMapProjection.Position(world, player);
            float q = (float)point.Q;
            float r = (float)point.R;
            float distance = Mathf.Max(Mathf.Abs(q), Mathf.Max(Mathf.Abs(r), Mathf.Abs(q + r)));
            PlayerDusk = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.12f, .88f, distance / Mathf.Max(1f, boardRadius)));
        }
        public void SetViewport(Rect screenRect)
        {
            // The scene camera keeps its existing full-screen viewport and UI stack.
            UpdateCamera();
        }

        public (Vector3 Focus, float Zoom) CaptureView() => (focus, zoom);
        public void RestoreView(Vector3 position, float scale)
        {
            focus = position; zoom = Mathf.Clamp(scale, .17f, MaxMapZoom); RebuildLinks(); UpdateCamera();
        }
        public void Focus(double q, double r) { focus = Position(q, r); zoom = Ink ? mapLayout.FocusZoom : .45f; RebuildLinks(); UpdateCamera(); }
        public void FocusActor(int id)
        {
            if (!pieces.TryGetValue(id, out var piece) || !piece.Root.gameObject.activeInHierarchy) return;
            focusedRouteActor = id == state.PlayerActorId ? null : id;
            focus = piece.Destination;
            zoom = Ink ? mapLayout.FocusZoom : .45f;
            RebuildLinks();
            RebuildActorRoutes();
            UpdateCamera();
        }

        public void Overview()
        {
            focus = origin;
            zoom = MaxMapZoom;
            focusedRouteActor = null;
            RebuildLinks();
            RebuildActorRoutes();
            UpdateCamera();
        }
        public void Zoom(float factor) { bool detailed = zoom < .65f; zoom = Mathf.Clamp(zoom * factor, .17f, MaxMapZoom); if (detailed != (zoom < .65f)) RebuildLinks(); UpdateCamera(); }

        public void Pan(Vector2 pixels)
        {
            var plane = new Plane(Vector3.up, focus);
            var center = boardCamera.pixelRect.center;
            var from = boardCamera.ScreenPointToRay(center);
            var to = boardCamera.ScreenPointToRay(center + new Vector2(pixels.x, -pixels.y));
            if (!plane.Raycast(from, out float fromDistance) || !plane.Raycast(to, out float toDistance)) return;
            focus += from.GetPoint(fromDistance) - to.GetPoint(toDistance);
            focus.x = Mathf.Clamp(focus.x, origin.x - unit * boardRadius * 2, origin.x + unit * boardRadius * 2);
            focus.z = Mathf.Clamp(focus.z, origin.z - unit * boardRadius * 2, origin.z + unit * boardRadius * 2);
            focus.y = origin.y;
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            float halfHeight = unit * (boardRadius + 1) * Mathf.Max(1.65f, 1.95f / boardCamera.aspect) * zoom;
            float distance = halfHeight / Mathf.Tan(mapVirtualCamera.m_Lens.FieldOfView * Mathf.Deg2Rad * .5f);
            // The camera is far above the ink board, so the space right next to the lens is empty.
            // Moving the near plane out preserves depth precision for thin landmark details.
            if (Ink) mapVirtualCamera.m_Lens.NearClipPlane = Mathf.Max(.3f, distance * .025f);
            mapVirtualCamera.m_Lens.FarClipPlane = distance + unit * (boardRadius + 1) * 6 + 600;
            mapVirtualCamera.transform.position = focus - mapVirtualCamera.transform.forward * distance;
        }

        public int? Pick(Vector2 screenPosition)
        {
            int? selected = null;
            float distance = float.PositiveInfinity;
            foreach (var pair in tiles)
            {
                if (!IsVisible(pair.Value)) continue;
                float lift = Ink ? InkLift(pair.Value, Time.unscaledTime) : pair.Value.LiftAt(Time.unscaledTime);
                var markerCenter = MarkerCenter(pair.Value, lift);
                var center = boardCamera.WorldToScreenPoint(markerCenter);
                if (center.z < boardCamera.nearClipPlane || center.z > boardCamera.farClipPlane) continue;
                float hitRadius = openingLinks.Count == 0 && pair.Value.Kind != MapMarkerKind.Trail
                    ? TypeMarkerRadius(boardCamera, markerCenter, pair.Value)
                    : Ink ? unit * InkRadius(pair.Key, pair.Value, lift)
                    : MarkerRadius(pair.Key, pair.Value, true, lift, picking: true);
                var edge = boardCamera.WorldToScreenPoint(markerCenter + boardCamera.transform.right * hitRadius);
                float radius = Vector2.Distance((Vector2)center, (Vector2)edge);
                float candidate = (screenPosition - (Vector2)center).sqrMagnitude;
                float target = Mathf.Max(10f, radius + 6f);
                if (candidate > target * target || candidate >= distance) continue;
                distance = candidate;
                selected = pair.Key;
            }
            return selected;
        }
        public Vector3 ProjectNode(int nodeId)
        {
            var tile = tiles[nodeId];
            float lift = Ink ? InkLift(tile, Time.unscaledTime) : tile.LiftAt(Time.unscaledTime);
            return boardCamera.WorldToScreenPoint(MarkerCenter(tile, lift));
        }
        private void OnEnable()
        {
            if (activeBoard == this && presentationSuspensions == 0) inkMap?.EnterMap(this, boardCamera);
        }
        private void OnDisable() => inkMap?.ExitMap(this);
        private void OnDestroy()
        {
            inkMap?.ExitMap(this);
            if (activeBoard == this) activeBoard = null;
            if (mapVirtualCamera != null) mapVirtualCamera.gameObject.SetActive(false);
            if (mapVirtualCamera != null && boardCamera != null) boardCamera.orthographic = originalOrthographic;
            foreach (var mesh in meshes) Destroy(mesh);

        }
    }
}
