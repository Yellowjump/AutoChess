using System.Collections.Generic;
using UnityEngine;

namespace WorldTimelineDebug
{
    internal sealed class WorldTimelineLandmarkMesh
    {
        private static readonly Color32 Stone = new Color32(142, 139, 132, 255);
        private static readonly Color32 Trim = new Color32(170, 165, 155, 255);
        private static readonly Color32 Timber = new Color32(101, 94, 85, 255);
        private static readonly Color32 Tile = new Color32(105, 105, 102, 255);
        private static readonly Color32 TileRib = new Color32(124, 122, 118, 255);
        private static readonly Color32 CaveShade = new Color32(61, 59, 56, 255);
        private static readonly Color32 PineInk = new Color32(83, 87, 79, 255);
        private static readonly Color32 Water = new Color32(166, 164, 157, 255);
        private static readonly Color32 Ice = new Color32(104, 150, 158, 106);
        private static readonly Color32 Frost = new Color32(173, 193, 194, 66);
        private static readonly Color32 Cinnabar = new Color32(150, 65, 48, 88);
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<int> triangles = new List<int>();

        internal static readonly Vector3 VolcanoVentOffset = new Vector3(.064f, 1.49f, -.213f);

        internal static Mesh Create(int kind)
        {
            var shape = new WorldTimelineLandmarkMesh();
            switch (kind)
            {
                case 0:
                    shape.Den();
                    break;
                case 1:
                    shape.Ferry();
                    break;
                case 2:
                    shape.Gate(true);
                    break;
                case 3:
                    shape.StelePine();
                    break;
                case 4:
                    shape.Gate(false);
                    break;
                case 5:
                    shape.VolcanicVent();
                    break;
                case 6:
                    shape.IceCavern();
                    break;
                case 7:
                    shape.InnerAltar();
                    break;
            }
            var mesh = new Mesh { name = "山河地标 · " + kind };
            mesh.SetVertices(shape.vertices);
            mesh.SetNormals(shape.normals);
            mesh.SetColors(shape.colors);
            mesh.SetTriangles(shape.triangles, 0);
            return mesh;
        }

        private void VolcanicVent()
        {
            const int sectors = 48;
            float[] radii = { .22f, .29f, .40f, .55f, .73f, .96f, 1.22f, 1.51f, 1.83f, 2.00f };
            float[] heights = { 1.50f, 1.43f, 1.24f, 1.00f, .75f, .53f, .34f, .20f, .096f, .006f };
            var charcoal = new Color32(53, 52, 49, 255);
            var scoria = new Color32(162, 154, 144, 255);
            var lavaEdge = new Color32(89, 48, 34, 78);
            var lavaCore = new Color32(193, 102, 62, 42);
            var hotLava = new Color32(224, 154, 104, 30);

            Vector3 Surface(float radial, float height, float angle)
            {
                float ridge = Mathf.Sin(angle * 8 + radial * 1.9f) * .59f
                    + Mathf.Cos(angle * 13 - radial * 2.4f) * .26f + Mathf.Sin(angle * 3) * .15f;
                float erodedRadius = radial * (1 + .066f * ridge + .044f * Mathf.Sin(3 * angle + .5f));
                float cut = Mathf.Sin(angle * 5 + .4f) * .023f;
                float depth = Mathf.Cos(angle);
                float slopeHeight = height + (ridge * .081f + cut) * Mathf.Sin(Mathf.PI * radial / radii[^1]);
                return new Vector3(.048f * height + Mathf.Sin(angle) * erodedRadius,
                    slopeHeight, -.16f * height + depth * erodedRadius * (depth > 0 ? .62f : .31f));
            }

            float Turn(float radial) => .04f + Mathf.Sin(radial * 4.2f - 1.4f) * .145f;

            Color32 RockTone(float angle, float radius)
            {
                float strata = Mathf.Sin(angle * 8 + radius * 1.9f) * .155f +
                    Mathf.Cos(angle * 13 - radius * 2.4f) * .09f;
                float exposure = .16f * Mathf.Cos(angle + .6f);
                return Color32.Lerp(charcoal, scoria, Mathf.Clamp01(.21f + radius * .22f + strata + exposure));
            }

            for (int sector = 0; sector < sectors; sector++)
            {
                float a = sector * 2 * Mathf.PI / sectors;
                float b = (sector + 1) * 2 * Mathf.PI / sectors;
                for (int ring = 0; ring < radii.Length - 1; ring++)
                {
                    var upperA = Surface(radii[ring], heights[ring], a);
                    var upperB = Surface(radii[ring], heights[ring], b);
                    var lowerA = Surface(radii[ring + 1], heights[ring + 1], a);
                    var lowerB = Surface(radii[ring + 1], heights[ring + 1], b);
                    var upperToneA = RockTone(a, radii[ring]);
                    var upperToneB = RockTone(b, radii[ring]);
                    var lowerToneA = RockTone(a, radii[ring + 1]);
                    var lowerToneB = RockTone(b, radii[ring + 1]);
                    int start = vertices.Count;
                    Quad(upperA, upperB, lowerB, lowerA, Vector3.up, charcoal);
                    for (int i = start; i < vertices.Count; i++)
                    {
                        Vector3 point = vertices[i];
                        colors[i] = point == upperA ? upperToneA : point == upperB ? upperToneB :
                            point == lowerA ? lowerToneA : lowerToneB;
                    }
                }

                var rimA = Surface(radii[0], heights[0], a);
                var rimB = Surface(radii[0], heights[0], b);
                var insideA = Surface(.15f, 1.325f, a);
                var insideB = Surface(.15f, 1.325f, b);
                Quad(insideA, insideB, rimB, rimA, Vector3.up, charcoal);
                Triangle(insideA, insideB, new Vector3(.064f, 1.315f, -.214f), Vector3.up,
                    sector % 4 == 0 ? lavaEdge : lavaCore);
            }

            for (int ring = 0; ring < radii.Length - 1; ring++)
            {
                for (int segment = 0; segment < 5; segment++)
                {
                    float a = segment / 5f;
                    float b = (segment + 1) / 5f;
                    Quad(Stream(a, -1, .023f), Stream(a, 1, .023f),
                        Stream(b, 1, .023f), Stream(b, -1, .023f), Vector3.up, lavaEdge);
                    Quad(Stream(a, -.42f, .030f), Stream(a, .48f, .030f),
                        Stream(b, .48f, .030f), Stream(b, -.42f, .030f), Vector3.up,
                        ring <= 1 ? hotLava : lavaCore);
                }

                Vector3 Stream(float fraction, float across, float lift)
                {
                    float radial = Mathf.Lerp(radii[ring], radii[ring + 1], fraction);
                    float height = Mathf.Lerp(heights[ring], heights[ring + 1], fraction);
                    float width = Mathf.Lerp(.177f, .020f, Mathf.InverseLerp(radii[0], radii[^1], radial));
                    return Surface(radial, height, Turn(radial) + width * across) + Vector3.up * lift;
                }
            }
        }

        private void IceCavern()
        {
            Den();
            for (int i = 0; i < 9; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var foot = new Vector3(side * (.58f + (i % 3) * .15f), .07f, .35f + (i / 2) * .11f);
                var tip = foot + new Vector3(side * .13f, .27f + (i % 4) * .11f, -.06f);
                var left = foot - Vector3.right * .085f;
                var right = foot + Vector3.right * .085f;
                var nose = foot + Vector3.forward * .08f;
                var back = foot - Vector3.forward * .067f;
                Triangle(left, nose, tip, Vector3.forward, Ice);
                Triangle(nose, right, tip, Vector3.forward, Frost);
                Triangle(right, back, tip, Vector3.back, Ice);
                Triangle(back, left, tip, Vector3.back, Frost);
            }
        }

        private void InnerAltar()
        {
            for (int i = 0; i < 3; i++)
                Box(0, .045f + i * .09f, -.13f, 1.90f - i * .35f, .09f, 1.38f - i * .28f, i % 2 == 0 ? Stone : Trim);
            Box(0, .77f, -.23f, .78f, 1.05f, .32f, CaveShade);
            Rock(new Vector3(0, 1.24f, -.23f), new Vector3(.42f, .16f, .18f), Timber);
            for (int i = 0; i < 4; i++)
                Box(.045f * (i % 2), 1.07f - .135f * i, -.064f, .29f - .027f * i, .035f, .015f, Cinnabar);
            foreach (float x in new[] { -.74f, .74f })
            {
                Box(x, .28f, .24f, .23f, .39f, .24f, Stone);
                Box(x, .48f, .24f, .25f, .04f, .27f, Cinnabar);
            }
        }

        private void Plinth(float width, float depth)
        {
            Box(0, .065f, 0, width, .13f, depth, Stone);
            Box(0, .145f, 0, width - .14f, .055f, depth - .14f, Trim);
        }

        private void Den()
        {
            const int segments = 12;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI / segments;
                float b = (i + 1) * Mathf.PI / segments;
                var sillA = CaveArch(a, false);
                var sillB = CaveArch(b, false);
                var browA = CaveArch(a, true);
                var browB = CaveArch(b, true);
                Quad(sillA, browA, browB, sillB, Vector3.forward, i % 3 == 0 ? Trim : Stone);
                var insideA = new Vector3(sillA.x * .73f, sillA.y * .72f, -.61f);
                var insideB = new Vector3(sillB.x * .73f, sillB.y * .72f, -.61f);
                Quad(sillA, sillB, insideB, insideA, Vector3.down, Timber);
                Triangle(insideA, insideB, new Vector3(0, .015f, -.65f), Vector3.forward, CaveShade);
                Quad(browA, browB, new Vector3(browB.x * .78f, browB.y * .93f, -.71f),
                    new Vector3(browA.x * .78f, browA.y * .93f, -.71f), Vector3.up, Stone);
            }
            Quad(new Vector3(-.60f, .012f, .29f), new Vector3(.60f, .012f, .21f),
                new Vector3(.44f, .014f, -.65f), new Vector3(-.44f, .014f, -.65f), Vector3.up, CaveShade);
            Rock(new Vector3(-.94f, 0, -.11f), new Vector3(.48f, .80f, .52f), Stone);
            Rock(new Vector3(.89f, 0, -.26f), new Vector3(.49f, .71f, .53f), Stone);
            Rock(new Vector3(-.29f, .52f, -.54f), new Vector3(.63f, .66f, .52f), Trim);
            Rock(new Vector3(.37f, .51f, -.62f), new Vector3(.60f, .50f, .45f), Stone);
            for (int i = 0; i < 8; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var scale = new Vector3(.16f + .025f * (i % 3), .13f - i * .008f, .15f);
                Rock(new Vector3(side * (.78f + .11f * Mathf.Sin(i * 3f)), 0, .30f + .088f * i), scale,
                    i % 3 == 0 ? Trim : Stone);
            }

            Vector3 CaveArch(float a, bool outer)
            {
                float chip = Mathf.Sin(a * 7f) * .052f + Mathf.Sin(a * 13f) * .021f;
                float x = Mathf.Cos(a) * (outer ? 1.08f : .65f);
                float y = .02f + Mathf.Sin(a) * (outer ? 1.12f : .73f);
                return new Vector3(x, y + chip, (outer ? .18f : .30f) + chip * 1.65f - x * .13f);
            }
        }

        private void StelePine()
        {
            Box(.28f, .07f, .23f, .56f, .14f, .43f, Stone);
            Box(.28f, .38f, .23f, .36f, .49f, .16f, Trim);
            Rock(new Vector3(.29f, .61f, .23f), new Vector3(.20f, .09f, .10f), Stone);
            var baseOfTree = new Vector3(-.32f, 0, -.18f);
            var bend = new Vector3(-.53f, .52f, -.26f);
            var crown = new Vector3(-.08f, 1.25f, -.27f);
            Trunk(baseOfTree, bend, .17f, .12f);
            Trunk(bend, crown, .12f, .05f);
            Trunk(bend, new Vector3(-.92f, .86f, .11f), .08f, .024f);
            Trunk(bend, new Vector3(.49f, .99f, -.40f), .09f, .022f);
            Canopy(crown, .67f, .39f);
            Canopy(new Vector3(-.85f, .84f, .12f), .49f, .33f);
            Canopy(new Vector3(.48f, .96f, -.40f), .55f, .34f);
            Trunk(baseOfTree + Vector3.up * .115f, new Vector3(.23f, .033f, .41f), .085f, .025f);
            Trunk(baseOfTree + Vector3.up * .11f, new Vector3(-.81f, .025f, .28f), .07f, .023f);
            for (int i = 0; i < 4; i++)
                Box(.29f, .32f + i * .066f, .312f, .10f - i % 2 * .025f, .016f, .008f, Timber);
            Rock(new Vector3(-.30f, 0, -.36f), new Vector3(.51f, .20f, .45f), Stone);
        }

        private void Canopy(Vector3 center, float width, float depth)
        {
            // Small overlapping sprays form the broad, uneven horizontal tiers of a pine.
            Rock(center, new Vector3(width, .22f, depth), PineInk);
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f;
                var spray = center + new Vector3(Mathf.Cos(angle) * width * .66f,
                    .035f * Mathf.Sin(i * 2.4f), Mathf.Sin(angle) * depth * .71f);
                Rock(spray, new Vector3(width * .35f, .09f + i % 3 * .028f, depth * .44f),
                    i % 3 == 0 ? Tile : PineInk);
            }
        }

        private void Rock(Vector3 center, Vector3 scale, Color32 color)
        {
            const int sides = 7;
            var peak = center + Vector3.Scale(scale, new Vector3(-.10f, 1f, .13f));
            for (int i = 0; i < sides; i++)
            {
                var a = RockRim(i, false);
                var b = RockRim(i + 1, false);
                var upperA = RockRim(i, true);
                var upperB = RockRim(i + 1, true);
                var outward = (a + b) * .5f - center;
                Quad(a, b, upperB, upperA, outward, color);
                Triangle(upperA, upperB, peak, outward + Vector3.up * scale.y, color);
            }

            Vector3 RockRim(int i, bool upper)
            {
                float angle = (i % sides) * Mathf.PI * 2f / sides;
                float radius = (.85f + .15f * Mathf.Sin(angle * 3f + center.x * 4f)) * (upper ? .70f : 1f);
                return center + Vector3.Scale(scale, new Vector3(Mathf.Cos(angle) * radius - (upper ? .14f : 0f),
                    upper ? .63f + .14f * Mathf.Sin(angle * 2f) : 0, Mathf.Sin(angle) * radius));
            }
        }

        private void Trunk(Vector3 start, Vector3 end, float baseRadius, float tipRadius)
        {
            var axis = (end - start).normalized;
            var right = Vector3.Cross(axis, Vector3.forward).normalized;
            var forward = Vector3.Cross(axis, right);
            const int sides = 7;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float next = (i + 1) * Mathf.PI * 2f / sides;
                var a = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                var b = right * Mathf.Cos(next) + forward * Mathf.Sin(next);
                Quad(start + a * baseRadius, start + b * baseRadius, end + b * tipRadius,
                    end + a * tipRadius, a + b, Timber);
            }
        }

        private void Ferry()
        {
            Plinth(2.68f, 1.55f);
            Box(-.36f, .183f, .01f, 1.36f, .03f, 1.30f, Water);
            for (int i = -1; i <= 1; i++)
                Box(-.36f + i * .29f, .201f, .43f + i * .13f, .19f, .014f, .038f, Trim);

            const int spans = 8;
            for (int i = 0; i < spans; i++)
            {
                float a = i / (float)spans, b = (i + 1f) / spans;
                float xa = -.36f + Mathf.Lerp(-.82f, .82f, a), xb = -.36f + Mathf.Lerp(-.82f, .82f, b);
                float ya = .22f + .47f * Mathf.Sin(a * Mathf.PI), yb = .22f + .47f * Mathf.Sin(b * Mathf.PI);
                Quad(new Vector3(xa, ya, -.20f), new Vector3(xa, ya, .36f), new Vector3(xb, yb, .36f),
                    new Vector3(xb, yb, -.20f), Vector3.up, (i % 2 == 0) ? Trim : Stone);
                for (int side = 0; side < 2; side++)
                {
                    float edge = side == 0 ? -.20f : .36f;
                    Quad(new Vector3(xa, ya - .11f, edge), new Vector3(xa, ya, edge),
                        new Vector3(xb, yb, edge), new Vector3(xb, yb - .11f, edge), Vector3.forward * edge, Stone);
                    if (i % 2 == 0) Box(xa, ya + .095f, edge, .054f, .19f, .060f, Trim);
                    Quad(new Vector3(xa, ya + .13f, edge), new Vector3(xa, ya + .18f, edge),
                        new Vector3(xb, yb + .18f, edge), new Vector3(xb, yb + .13f, edge), Vector3.forward * edge, Timber);
                }
            }
            Box(.93f, .24f, -.18f, .58f, .13f, .65f, Stone);
            foreach (float x in new[] { .70f, 1.16f })
            foreach (float z in new[] { -.43f, .07f })
                Box(x, .51f, z, .074f, .44f, .074f, Timber);
            Roof(.93f, .73f, -.18f, .86f, .83f, .24f);
        }

        private void Gate(bool fortified)
        {
            Box(0, .036f, .22f, 1.18f, .072f, .98f, Stone);
            Box(0, .021f, .77f, .96f, .042f, .19f, Trim);
            foreach (float x in new[] { -.70f, .70f })
            {
                Box(x, .072f, 0, .62f, .144f, .71f, Stone);
                Box(x, .153f, 0, .53f, .045f, .62f, Trim);
                Box(x, .24f, 0, .48f, .15f, .56f, Trim);
                Box(x, .66f, 0, .31f, .77f, .42f, Stone);
                Box(x, 1.08f, 0, .45f, .15f, .54f, Trim);
                if (fortified)
                {
                    Box(x * 1.57f, .31f, -.24f, .57f, .62f, .60f, Stone);
                    Box(x * 1.57f, .63f, -.24f, .64f, .10f, .67f, Tile);
                }
            }
            Box(0, 1.21f, 0, 1.88f, .16f, .47f, Stone);
            const int archSegments = 12;
            for (int i = 0; i < archSegments; i++)
            {
                float a = i * Mathf.PI / archSegments;
                float b = (i + 1) * Mathf.PI / archSegments;
                var aInner = new Vector3(.46f * Mathf.Cos(a), .70f + .39f * Mathf.Sin(a), .245f);
                var bInner = new Vector3(.46f * Mathf.Cos(b), .70f + .39f * Mathf.Sin(b), .245f);
                var aOuter = new Vector3(.60f * Mathf.Cos(a), .70f + .51f * Mathf.Sin(a), .245f);
                var bOuter = new Vector3(.60f * Mathf.Cos(b), .70f + .51f * Mathf.Sin(b), .245f);
                Quad(aInner, aOuter, bOuter, bInner, Vector3.forward, i % 2 == 0 ? Stone : Trim);
                var thickness = Vector3.back * .49f;
                Quad(aInner + thickness, aOuter + thickness, bOuter + thickness, bInner + thickness,
                    Vector3.back, i % 2 == 0 ? Stone : Trim);
                Quad(aInner + thickness, bInner + thickness, bInner, aInner, Vector3.down, Stone);
            }
            Box(0, 1.28f, 0, 2.0f, .07f, .57f, Timber);
            foreach (float x in new[] { -.79f, -.62f, .62f, .79f })
                Box(x, 1.288f, .28f, .085f, .070f, .17f, Timber);
            Roof(0, 1.32f, 0, 2.26f, .98f, .35f);
        }

        private void Roof(float x, float y, float z, float width, float depth, float rise)
        {
            const int columns = 14;
            const int rows = 5;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int column = 0; column < columns; column++)
                {
                    float u = -1f + 2f * column / columns;
                    float nextU = u + 2f / columns;
                    for (int row = 0; row < rows; row++)
                    {
                        float t = row / (float)rows;
                        float nextT = (row + 1f) / rows;
                        var a = Surface(u, t, side);
                        var b = Surface(nextU, t, side);
                        var c = Surface(nextU, nextT, side);
                        var d = Surface(u, nextT, side);
                        var seam = Vector3.right * (width * .075f / columns);
                        var lift = Vector3.up * .012f;
                        Quad(a, b, c, d, Vector3.up, Tile);
                        Quad(a + lift, a + seam + lift, d + seam + lift, d + lift, Vector3.up, TileRib);
                        if (row == 0)
                            Quad(a, b, b - Vector3.up * .052f, a - Vector3.up * .052f, Vector3.forward * side, Timber);
                        if (column == 0 || column == columns - 1)
                        {
                            var edgeA = column == 0 ? a : b;
                            var edgeB = column == 0 ? d : c;
                            Quad(edgeA, edgeB, edgeB - Vector3.up * .055f, edgeA - Vector3.up * .055f,
                                column == 0 ? Vector3.left : Vector3.right, Timber);
                        }
                    }
                }
            }
            for (int segment = 0; segment < columns; segment++)
            {
                float u = -1f + 2f * segment / columns;
                var a = Surface(u, 1f, 1) + Vector3.up * .015f;
                var b = Surface(u + 2f / columns, 1f, 1) + Vector3.up * .015f;
                var rim = Vector3.forward * .038f;
                Quad(a - rim, a + rim, b + rim, b - rim, Vector3.up, TileRib);
            }

            Vector3 Surface(float u, float t, int side)
            {
                float tip = Mathf.Pow(Mathf.Abs(u), 4f);
                return new Vector3(x + u * width * .5f * Mathf.Lerp(1f, .87f, t),
                    y + rise * Mathf.Pow(t, 1.65f) + tip * Mathf.Lerp(.18f, .08f, t),
                    z + side * depth * .5f * (1f - t) * (1f + .13f * tip));
            }
        }

        private void Box(float x, float y, float z, float width, float height, float depth, Color32 color)
        {
            var p = new Vector3(x, y, z);
            var dx = Vector3.right * (width * .5f);
            var dy = Vector3.up * (height * .5f);
            var dz = Vector3.forward * (depth * .5f);
            Quad(p + dy - dx - dz, p + dy - dx + dz, p + dy + dx + dz, p + dy + dx - dz, Vector3.up, color);
            Quad(p - dy - dx + dz, p + dy - dx + dz, p + dy + dx + dz, p - dy + dx + dz, Vector3.forward, color);
            Quad(p - dy - dx - dz, p + dy - dx - dz, p + dy + dx - dz, p - dy + dx - dz, Vector3.back, color);
            Quad(p - dx - dy - dz, p - dx + dy - dz, p - dx + dy + dz, p - dx - dy + dz, Vector3.left, color);
            Quad(p + dx - dy - dz, p + dx + dy - dz, p + dx + dy + dz, p + dx - dy + dz, Vector3.right, color);
        }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color32 color)
        {
            // A roof can have lifted corners, so its two triangles get separate normals.
            Triangle(a, b, c, outward, color);
            Triangle(a, c, d, outward, color);
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color32 color)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            var first = vertices.Count;
            if (Vector3.Dot(normal, outward) < 0)
            {
                (b, c) = (c, b);
                normal = -normal;
            }
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            for (int i = 0; i < 3; i++)
            {
                normals.Add(normal); colors.Add(color); triangles.Add(first + i);
            }
        }
    }
}
