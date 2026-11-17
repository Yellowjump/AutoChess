using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineBoard
    {
        private enum PieceKind { Player, Chosen, History }

        private sealed class MapPiece
        {
            public Transform Root;
            public PieceKind Kind;
            public Vector3 From, Destination;
            private float startedAt, duration;

            public float Progress(float now) => duration > 0f ? Mathf.Clamp01((now - startedAt) / duration) : 1f;
            public float Landing(float now) => duration > 0f ? now - startedAt - duration : float.PositiveInfinity;

            public void MoveTo(Vector3 position, float now, float seconds)
            {
                if (!Root.gameObject.activeSelf)
                {
                    Root.position = From = Destination = position;
                    duration = 0f;
                    Root.gameObject.SetActive(true);
                    return;
                }
                if ((Destination - position).sqrMagnitude < .0001f) return;
                From = Root.position;
                Destination = position;
                startedAt = now;
                duration = seconds;
            }
        }

        private readonly Dictionary<int, MapPiece> pieces = new Dictionary<int, MapPiece>();
        private readonly Mesh[] pieceMeshes = new Mesh[3];
        private static readonly Color[] PieceInk =
        {
            new Color(.053f, .177f, .151f), new Color(.29f, .091f, .048f), new Color(.127f, .145f, .194f)
        };
        private static readonly Color[] PieceLine =
        {
            new Color(.49f, .62f, .49f), new Color(.77f, .51f, .29f), new Color(.55f, .59f, .66f)
        };
        private static readonly Color Paper = new Color(.88f, .80f, .65f);

        private MapPiece CreatePiece(int id, int playerId, int? chosenId)
        {
            var kind = id == playerId ? PieceKind.Player : id == chosenId ? PieceKind.Chosen : PieceKind.History;
            var root = new GameObject("棋子 #" + id).transform;
            root.SetParent(transform, false);
            root.gameObject.SetActive(false);
            if (kind == PieceKind.History)
            {
                var label = new GameObject("编号").AddComponent<TextMesh>();
                int mapLayer = LayerMask.NameToLayer(WorldTimelineInkMap.LayerName);
                if (Ink && mapLayer >= 0) label.gameObject.layer = mapLayer;
                label.transform.SetParent(root, false);
                label.transform.localPosition = Vector3.up * unit * .76f;
                label.transform.rotation = mapVirtualCamera.transform.rotation;
                label.text = "#" + id;
                label.anchor = TextAnchor.MiddleCenter;
                label.characterSize = unit * .082f;
                label.fontSize = 48;
                label.color = Ink ? new Color(.19f, .22f, .24f) : Color.white;
            }
            int index = (int)kind;
            if (pieceMeshes[index] == null)
            {
                pieceMeshes[index] = BuildPieceMesh(kind);
                meshes.Add(pieceMeshes[index]);
            }
            return new MapPiece { Root = root, Kind = kind };
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            foreach (var piece in pieces.Values)
            {
                if (!piece.Root.gameObject.activeSelf) continue;
                float progress = piece.Progress(now);
                piece.Root.position = Vector3.Lerp(piece.From, piece.Destination, Mathf.SmoothStep(0f, 1f, progress));
            }
        }

        private void DrawPieces(CommandBuffer cmd, Camera camera)
        {
            float now = Time.unscaledTime;
            DrawPieceFootprints(cmd, now);
            foreach (var piece in pieces.Values)
            {
                if (!piece.Root.gameObject.activeInHierarchy) continue;
                float progress = piece.Progress(now);
                float lift = .40f + .07f * Mathf.Sin(progress * Mathf.PI);
                float scale = piece.Kind == PieceKind.History ? .82f : 1f;
                var matrix = Matrix4x4.TRS(piece.Root.position + Vector3.up * (lift * unit),
                    camera.transform.rotation, Vector3.one * (scale * unit));
                cmd.DrawMesh(pieceMeshes[(int)piece.Kind], matrix, overlay, 0, 1);
            }
        }

        private void DrawPieceFootprints(CommandBuffer cmd, float now)
        {
            int count = 0;
            foreach (var piece in pieces.Values)
            {
                if (!piece.Root.gameObject.activeInHierarchy) continue;
                AddMarker(cmd, markerMesh, Matrix4x4.TRS(piece.Root.position, Quaternion.identity,
                    new Vector3(.23f, 1f, .17f) * unit), new Color(.16f, .15f, .10f, .19f), ref count);
            }
            if (count > 0) DrawMarkerBatch(cmd, markerMesh, count);

            count = 0;
            foreach (var entry in pieces)
            {
                var piece = entry.Value;
                if (piece.Kind == PieceKind.Player || !piece.Root.gameObject.activeInHierarchy) continue;
                var tint = ActorRouteColor(entry.Key, state.ChosenActorId);
                AddMarker(cmd, pulseMesh, Matrix4x4.TRS(piece.Root.position, Quaternion.identity,
                    Vector3.one * (.27f * unit)), tint, ref count);
            }
            if (count > 0) DrawMarkerBatch(cmd, pulseMesh, count);

            count = 0;
            foreach (var piece in pieces.Values)
            {
                if (!piece.Root.gameObject.activeInHierarchy) continue;
                float landing = piece.Landing(now);
                bool landed = landing >= 0f && landing < .42f;
                if (!landed && piece.Kind != PieceKind.Player) continue;
                float progress = Mathf.Clamp01(landing / .42f);
                float radius = landed ? Mathf.Lerp(.27f, .48f, progress) : .30f;
                var tint = PieceInk[(int)piece.Kind];
                tint.a = landed ? .42f * (1f - progress) : .28f;
                AddMarker(cmd, pulseMesh, Matrix4x4.TRS(piece.Root.position, Quaternion.identity,
                    Vector3.one * (radius * unit)), tint, ref count);
            }
            if (count > 0) DrawMarkerBatch(cmd, pulseMesh, count);
        }

        private static Mesh BuildPieceMesh(PieceKind kind)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var ink = PieceInk[(int)kind];
            var line = PieceLine[(int)kind];
            var edge = Color.Lerp(ink, new Color(.037f, .032f, .026f), .48f);
            Ring(0f, .303f, edge);
            Ring(.273f, .290f, Paper);
            Ring(0f, .274f, ink);
            Ring(.223f, .230f, line);

            switch (kind)
            {
                case PieceKind.Player:
                    // An angled sword gives the player's seal a distinct silhouette at map scale.
                    Glyph(-35f, Paper, new Vector2(-.031f, -.041f), new Vector2(.031f, -.041f),
                        new Vector2(.033f, .110f), new Vector2(0f, .168f), new Vector2(-.033f, .110f));
                    Glyph(-35f, Paper, new Vector2(-.078f, -.062f), new Vector2(.078f, -.062f),
                        new Vector2(.065f, -.037f), new Vector2(-.065f, -.037f));
                    Glyph(-35f, line, new Vector2(-.020f, -.138f), new Vector2(.020f, -.138f),
                        new Vector2(.018f, -.065f), new Vector2(-.018f, -.065f));
                    break;
                case PieceKind.Chosen:
                    Ring(.112f, .123f, line);
                    for (int i = 0; i < 4; i++)
                        Glyph(i * 90f, Paper, Vector2.zero, new Vector2(-.047f, .043f),
                            new Vector2(0f, .171f), new Vector2(.047f, .043f));
                    break;
                default:
                    Glyph(0f, Paper, new Vector2(-.132f, -.088f), new Vector2(-.018f, -.121f),
                        new Vector2(-.018f, .084f), new Vector2(-.132f, .118f));
                    Glyph(0f, Paper, new Vector2(.018f, -.121f), new Vector2(.132f, -.088f),
                        new Vector2(.132f, .118f), new Vector2(.018f, .084f));
                    Glyph(0f, line, new Vector2(-.103f, .046f), new Vector2(-.048f, .033f),
                        new Vector2(-.048f, .055f), new Vector2(-.103f, .069f));
                    Glyph(0f, line, new Vector2(.047f, .004f), new Vector2(.104f, .019f),
                        new Vector2(.104f, .042f), new Vector2(.047f, .026f));
                    break;
            }

            var mesh = new Mesh { name = "轮回角色圆印 " + kind };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            return mesh;

            void Ring(float inner, float outer, Color color)
            {
                const int sides = 32;
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    float next = (i + 1) * Mathf.PI * 2f / sides;
                    Vector2 a = StampEdge(angle), b = StampEdge(next);
                    Face(a * inner, a * outer, b * outer, color);
                    if (inner > 0f) Face(a * inner, b * outer, b * inner, color);
                }
            }

            Vector2 StampEdge(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                (1f + .013f * Mathf.Sin(5f * angle) + .009f * Mathf.Cos(9f * angle));

            void Glyph(float angle, Color color, params Vector2[] points)
            {
                float radians = angle * Mathf.Deg2Rad;
                float sine = Mathf.Sin(radians), cosine = Mathf.Cos(radians);
                Vector2 Rotate(Vector2 p) => new Vector2(p.x * cosine - p.y * sine, p.x * sine + p.y * cosine);
                for (int i = 1; i + 1 < points.Length; i++)
                    Face(Rotate(points[0]), Rotate(points[i]), Rotate(points[i + 1]), color);
            }

            void Face(Vector2 a, Vector2 b, Vector2 c, Color color)
            {
                int index = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                colors.Add(color); colors.Add(color); colors.Add(color);
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            }
        }
    }
}
