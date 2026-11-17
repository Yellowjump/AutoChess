using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public sealed partial class WorldTimelineBoard
    {
        private static readonly Color ChosenRouteColor = new Color32(156, 103, 20, 255);
        private static readonly Color[] HistoryRouteColors =
        {
            new Color32(36, 100, 177, 255), new Color32(151, 55, 122, 255),
            new Color32(30, 125, 105, 255), new Color32(186, 69, 44, 255),
            new Color32(103, 65, 167, 255), new Color32(109, 124, 36, 255),
            new Color32(0, 120, 155, 255), new Color32(182, 71, 102, 255),
            new Color32(101, 72, 51, 255), new Color32(63, 81, 128, 255)
        };

        private Mesh actorRouteMesh;
        private int? focusedRouteActor;
        private WorldSimulation routeSimulation;
        private readonly List<Vector3> actorRouteVertices = new List<Vector3>();
        private readonly List<Color> actorRouteColors = new List<Color>();
        private readonly List<int> actorRouteIndices = new List<int>();

        public static Color ActorRouteColor(int actorId, int? chosenId)
        {
            if (actorId == chosenId) return ChosenRouteColor;
            int index = ((actorId - 10) % HistoryRouteColors.Length + HistoryRouteColors.Length) % HistoryRouteColors.Length;
            return HistoryRouteColors[index];
        }

        private void RebuildActorRoutes()
        {
            if (routeSimulation == null) return;
            actorRouteVertices.Clear();
            actorRouteColors.Clear();
            actorRouteIndices.Clear();

            var routes = ActorMapProjection.Routes(routeSimulation);
            var sharedEdges = new Dictionary<(int From, int To), List<(int Actor, ActorMapProjection.LegProgress Progress)>>();
            foreach (var route in routes.OrderBy(route => route.Key))
                foreach (var leg in route.Value)
                {
                    if (!sharedEdges.TryGetValue(leg.Key, out var lanes))
                        sharedEdges.Add(leg.Key, lanes = new List<(int, ActorMapProjection.LegProgress)>());
                    lanes.Add((route.Key, leg.Value));
                }

            bool hasFocus = focusedRouteActor.HasValue && routes.TryGetValue(focusedRouteActor.Value, out var focused)
                && focused.Count > 0;
            foreach (var edge in sharedEdges)
            {
                Vector3 from = tiles[edge.Key.From].Center;
                Vector3 to = tiles[edge.Key.To].Center;
                var across = Vector3.Cross(Vector3.up, to - from).normalized * unit;
                bool opening = openingLinks.ContainsKey(edge.Key);
                var lanes = edge.Value;
                float width = lanes.Count > 5 ? .012f : .018f;
                for (int i = 0; i < lanes.Count; i++)
                {
                    var lane = lanes[i];
                    float offset = (i - (lanes.Count - 1) * .5f) * (width * 2f + .016f);
                    // Keep the opening brushstroke clear down the middle of shared edges.
                    if (opening) offset += offset < 0 ? -.065f : .065f;
                    Vector3 shift = across * offset + Vector3.up * (unit * .018f);
                    Color color = ActorRouteColor(lane.Actor, state.ChosenActorId);
                    bool travelled = lane.Progress == ActorMapProjection.LegProgress.Travelled;
                    bool highlighted = hasFocus && lane.Actor == focusedRouteActor;
                    color.a = hasFocus && !highlighted ? .37f : travelled || highlighted ? .95f : .78f;
                    Vector3 side = across * (highlighted ? width * 1.18f : width);
                    if (travelled)
                        AddRouteStrip(Vector3.Lerp(from, to, .055f) + shift, Vector3.Lerp(from, to, .945f) + shift, side, color);
                    else
                        for (int dash = 0; dash < 4; dash++)
                        {
                            float start = .055f + dash * .23f;
                            AddRouteStrip(Vector3.Lerp(from, to, start) + shift,
                                Vector3.Lerp(from, to, start + .145f) + shift, side, color);
                        }
                }
            }

            actorRouteMesh.Clear();
            actorRouteMesh.SetVertices(actorRouteVertices);
            actorRouteMesh.SetColors(actorRouteColors);
            actorRouteMesh.SetTriangles(actorRouteIndices, 0);
        }

        private void AddRouteStrip(Vector3 from, Vector3 to, Vector3 side, Color color)
        {
            int first = actorRouteVertices.Count;
            actorRouteVertices.Add(from - side);
            actorRouteVertices.Add(from + side);
            actorRouteVertices.Add(to - side);
            actorRouteVertices.Add(to + side);
            for (int i = 0; i < 4; i++) actorRouteColors.Add(color);
            actorRouteIndices.Add(first);
            actorRouteIndices.Add(first + 1);
            actorRouteIndices.Add(first + 2);
            actorRouteIndices.Add(first + 1);
            actorRouteIndices.Add(first + 3);
            actorRouteIndices.Add(first + 2);
        }

        private void DrawActorRoutes(CommandBuffer cmd)
        {
            if (actorRouteIndices.Count > 0) cmd.DrawMesh(actorRouteMesh, Matrix4x4.identity, overlay, 0, 1);
        }
    }
}
