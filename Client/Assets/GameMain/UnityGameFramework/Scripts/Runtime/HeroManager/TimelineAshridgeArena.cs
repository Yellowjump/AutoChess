using System.Collections.Generic;
using UnityEngine;

namespace UnityGameFramework.Runtime
{
    internal static class TimelineAshridgeArena
    {
        private static readonly Color Ash = new Color(.52f, .51f, .49f, 0f);
        private static readonly Color Basalt = new Color(.026f, .028f, .032f, .35f);
        private static readonly Color Sky = new Color(.46f, .47f, .50f, .70f);
        private static readonly Color Lava = new Color(1f, .28f, .046f, 1f);
        private static readonly Vector2[] QuadUv = { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };

        internal static Mesh Build(bool withMist)
        {
            var surface = new ArenaMesh();
            surface.BuildGround();
            surface.Quad(new Vector3(-128, -16, 97), new Vector3(-128, 50, 97),
                new Vector3(128, 50, 97), new Vector3(128, -16, 97), Sky, QuadUv);

            // Low shoulders leave the near combat lanes open. Mid-distance ridges carry the sharp silhouettes.
            surface.Peak(-7.2f, -.6f, 1.85f, 2.34f, 1.38f, 10);
            surface.Peak(7.35f, -.1f, 1.72f, 2.24f, 1.63f, 35);
            surface.Ridge(-6.8f, 6.1f, 2.15f, 3.12f, 3.78f, 13, true);
            surface.Ridge(7.4f, 7.4f, 2.55f, 3.50f, 4.13f, 39);
            surface.Ridge(-10.8f, 12.4f, 3.12f, 4.50f, 5.58f, 20);
            surface.Peak(-4.65f, 12.7f, 1.23f, 2.13f, 2.78f, 31);
            surface.Peak(4.85f, 14.6f, 1.40f, 2.65f, 3.10f, 43);
            surface.Ridge(10.3f, 16.5f, 3.55f, 4.92f, 6.31f, 48);
            surface.Ridge(-13.9f, 24.0f, 3.67f, 4.90f, 7.95f, 24);
            surface.Ridge(-8.5f, 27.6f, 3.44f, 4.85f, 6.85f, 63);
            surface.Peak(-2.85f, 24.5f, 1.76f, 2.53f, 3.04f, 66);
            surface.Peak(2.95f, 27.1f, 1.71f, 3.11f, 4.12f, 68);
            surface.Ridge(7.5f, 34.8f, 3.90f, 5.44f, 8.62f, 51, true);
            surface.Ridge(13.0f, 36.2f, 4.72f, 5.74f, 7.04f, 54);
            surface.Peak(18.3f, 33.2f, 4.38f, 5.11f, 6.66f, 59);

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 11; col++)
                {
                    int i = row * 11 + col;
                    float x = (col - 5) * (7.3f + row * .9f) + Mathf.Sin(i * 2.3f) * 2.1f + row * 2.5f;
                    float z = 43.5f + row * 16.0f + Mathf.Cos(i * 3.8f) * 3.9f;
                    float shoulder = Mathf.SmoothStep(.26f, 1f, Mathf.Abs(col - 5) / 3.6f);
                    float height = (4.3f + i % 6 * 1.18f + row * 1.52f) * shoulder;
                    surface.Peak(x, z, 3.7f + i % 4 * .83f, 4.7f + i % 3, height, 180 + i);
                }
            }
            for (int i = 0; i < 26; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float z = -3.5f + i * .68f;
                float x = side * (4.35f + .96f * Mathf.PerlinNoise(i * .63f, 8.4f));
                surface.Peak(x, z, .30f + i % 4 * .16f, .30f + i % 3 * .16f, .21f + i % 5 * .14f, 121 + i);
            }

            surface.GroundSeam(new Vector2(-4.7f, -4.5f), new Vector2(-4.15f, -1.1f),
                new Vector2(-5.05f, 2.45f), .13f);
            surface.GroundSeam(new Vector2(5.14f, 1.65f), new Vector2(4.82f, 4.76f),
                new Vector2(6.25f, 6.70f), .12f);
            surface.GroundSeam(new Vector2(3.65f, 7.86f), new Vector2(3.25f, 9.38f),
                new Vector2(3.82f, 11.86f), .07f);

            if (withMist)
            {
                surface.Mist(1f, 65f, 104f, 8.0f, .58f);
                surface.Mist(-7f, 49f, 79f, 6.0f, .62f);
                surface.Mist(4.5f, 37f, 54f, 5.2f, .62f);
                surface.Mist(-9f, 29f, 36f, 3.4f, .55f);
                surface.Mist(8f, 24f, 30f, 3.0f, .51f);
                surface.Mist(-7.2f, 18.4f, 24f, 2.35f, .54f);
                surface.Mist(3.5f, 12.8f, 21f, 1.52f, .49f);
                surface.Mist(-5.0f, 6.7f, 9f, 1.16f, .35f);
                surface.Mist(6.5f, 5.2f, 8f, .87f, .35f);
                surface.Mist(-6.1f, .35f, 5.4f, .78f, .29f);
                surface.Mist(6.0f, -.85f, 4.7f, .62f, .27f);
            }
            return surface.Finish(withMist);
        }

        private static float GroundHeight(float x, float z)
        {
            float edge = Mathf.Clamp01((Mathf.Abs(x) - 3.95f) / 4.8f);
            float beyond = Mathf.Clamp01((Mathf.Abs(z) - 4.7f) / 5.8f);
            float relief = Mathf.Max(edge, beyond * .78f);
            float texture = Mathf.PerlinNoise(x * .33f + 6.2f, z * .27f + 8.3f);
            return -.045f + relief * ((texture - .54f) * 1.29f - .29f);
        }

        private sealed class ArenaMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> opaque = new List<int>();
            private readonly List<int> mist = new List<int>();

            internal void BuildGround()
            {
                const int columns = 113, rows = 137;
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < columns; col++)
                    {
                        float x = Mathf.Lerp(-56, 56, col / (columns - 1f));
                        float z = Mathf.Lerp(-17, 99, row / (rows - 1f));
                        Add(new Vector3(x, GroundHeight(x, z), z), Ash, Vector2.zero);
                        if (row == 0 || col == 0) continue;
                        int d = row * columns + col;
                        Triangle(opaque, d - columns - 1, d - 1, d);
                        Triangle(opaque, d - columns - 1, d, d - columns);
                    }
            }

            internal void Peak(float x, float z, float width, float depth, float height, int seed, bool glowing = false)
            {
                const int facets = 12;
                var random = new System.Random(seed);
                float Range(float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());
                var foot = new Vector3(x, GroundHeight(x, z) - .17f, z);
                float hand = seed % 2 == 0 ? -1f : 1f;
                var lean = new Vector3(hand * width * Range(.29f, .62f), 0, depth * Range(-.29f, .28f));
                float angleOffset = Range(-.21f, .21f);
                var rings = new Vector3[4, facets];
                for (int facet = 0; facet < facets; facet++)
                {
                    float angle = 2f * Mathf.PI * facet / facets + angleOffset + Range(-.16f, .16f);
                    float flute = facet % 3 == 1 ? Range(.49f, .68f) : Range(.84f, 1.14f);
                    var edge = new Vector3(Mathf.Cos(angle) * width, 0, Mathf.Sin(angle) * depth) * flute;
                    float spine = Mathf.Max(0f, Mathf.Cos(angle) * -hand);
                    rings[0, facet] = foot + edge;
                    rings[0, facet].y = GroundHeight(rings[0, facet].x, rings[0, facet].z) - .12f;
                    rings[1, facet] = foot + edge * Range(.65f, .88f) + lean * .19f
                        + Vector3.up * height * (Range(.22f, .39f) + spine * .09f);
                    rings[2, facet] = foot + edge * Range(.24f, .49f) + lean * .49f
                        + Vector3.up * height * (Range(.48f, .61f) + spine * .13f);
                    rings[3, facet] = foot + edge * Range(.082f, .23f) + lean * .70f
                        + Vector3.up * height * (Range(.69f, .79f) + spine * .11f);
                }
                var summit = foot + lean + Vector3.up * height;
                for (int facet = 0; facet < facets; facet++)
                {
                    int next = (facet + 1) % facets;
                    float tint = Range(.73f, 1.34f);
                    for (int layer = 0; layer < 3; layer++)
                    {
                        var a = rings[layer, facet];
                        var b = rings[layer, next];
                        var c = rings[layer + 1, next];
                        var d = rings[layer + 1, facet];
                        RockFace(a, b, d, tint * (1f + layer * .09f));
                        RockFace(b, c, d, tint * Range(.80f, 1.08f));
                    }
                    RockFace(rings[3, facet], rings[3, next], summit, tint * 1.17f);
                }
                if (glowing) PeakSeam(rings, facets, height);
            }

            internal void Ridge(float x, float z, float width, float depth, float height, int seed, bool glowing = false)
            {
                Peak(x, z, width, depth, height, seed, glowing);
                Peak(x - width * .73f, z + depth * .27f, width * .49f, depth * .74f, height * .69f, seed + 70);
                Peak(x + width * .78f, z + depth * .48f, width * .55f, depth * .76f, height * .48f, seed + 71);
            }

            private void RockFace(Vector3 a, Vector3 b, Vector3 c, float tint)
            {
                Vector3 normal = Vector3.Cross(c - a, b - a).normalized;
                float light = .60f + .65f * Mathf.Clamp01(Vector3.Dot(normal, new Vector3(-.46f, .74f, -.49f)));
                var color = new Color(Basalt.r * tint * light, Basalt.g * tint * light, Basalt.b * tint * light, Basalt.a);
                int first = vertices.Count;
                Add(a, color, Vector2.zero);
                Add(b, color, Vector2.zero);
                Add(c, color, Vector2.zero);
                Triangle(opaque, first, first + 1, first + 2);
            }

            private void PeakSeam(Vector3[,] rings, int facets, float height)
            {
                const int face = 9;
                int next = (face + 1) % facets;
                float width = Mathf.Clamp(height * .024f, .075f, .16f);
                for (int tier = 0; tier < 3; tier++)
                {
                    var a = rings[tier, face];
                    var b = rings[tier, next];
                    var c = rings[tier + 1, next];
                    var d = rings[tier + 1, face];
                    // Cross the face diagonal in two pieces so both strips sit on the rock.
                    var low = Vector3.Lerp(a, b, .36f + .10f * (tier % 2));
                    var mid = Vector3.Lerp(b, d, .46f + .08f * (tier % 2));
                    var high = Vector3.Lerp(d, c, .36f + .10f * ((tier + 1) % 2));
                    SeamFace(low, mid, b - a, width * (1f - tier * .17f));
                    SeamFace(mid, high, c - d, width * (.94f - tier * .17f));
                }
            }

            private void SeamFace(Vector3 from, Vector3 to, Vector3 across, float width)
            {
                Vector3 edge = across.normalized * width;
                Vector3 lift = new Vector3(0, .009f, -.025f);
                Quad(from - edge + lift, to - edge * .75f + lift, to + edge * .75f + lift, from + edge + lift, Lava, QuadUv);
            }

            internal void GroundSeam(Vector2 start, Vector2 bend, Vector2 end, float width)
            {
                const int samples = 26;
                Vector3 previousLeft = default, previousRight = default;
                for (int i = 0; i <= samples; i++)
                {
                    float t = i / (float)samples;
                    var point = Vector2.Lerp(Vector2.Lerp(start, bend, t), Vector2.Lerp(bend, end, t), t);
                    point.x += Mathf.Sin(t * 15.7f) * .14f + Mathf.Sin(t * 31.4f) * .042f;
                    float edge = width * Mathf.Max(.08f, Mathf.Sin(Mathf.PI * t)) * (.68f + .32f * Mathf.Sin(t * 39f));
                    var left = new Vector3(point.x - edge, GroundHeight(point.x - edge, point.y) + .041f, point.y);
                    var right = new Vector3(point.x + edge, GroundHeight(point.x + edge, point.y) + .041f, point.y);
                    if (i != 0) Quad(previousLeft, left, right, previousRight, Lava, QuadUv);
                    previousLeft = left;
                    previousRight = right;
                }
            }

            internal void Mist(float x, float z, float width, float height, float opacity)
            {
                float left = x - width * .5f, right = x + width * .5f;
                float low = -.38f;
                Quad(new Vector3(left, low, z), new Vector3(left, low + height, z + .36f),
                    new Vector3(right, low + height, z + .36f), new Vector3(right, low, z),
                    new Color(.51f, .53f, .57f, opacity), QuadUv, mist);
            }

            internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector2[] coords,
                List<int> triangles = null)
            {
                int start = vertices.Count;
                Add(a, color, coords[0]);
                Add(b, color, coords[1]);
                Add(c, color, coords[2]);
                Add(d, color, coords[3]);
                var target = triangles ?? opaque;
                Triangle(target, start, start + 1, start + 2);
                Triangle(target, start, start + 2, start + 3);
            }

            private static void Triangle(List<int> triangles, int a, int b, int c)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }

            private void Add(Vector3 position, Color color, Vector2 coords)
            {
                vertices.Add(position);
                colors.Add(color);
                uv.Add(coords);
            }

            internal Mesh Finish(bool withMist)
            {
                var mesh = new Mesh { name = "Ashridge Pass terrain", subMeshCount = withMist ? 2 : 1 };
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(opaque, 0);
                if (withMist) mesh.SetTriangles(mist, 1);
                return mesh;
            }
        }
    }
}
