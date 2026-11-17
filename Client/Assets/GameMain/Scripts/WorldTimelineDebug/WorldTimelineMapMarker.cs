using UnityEngine;
using WorldTimeline;

namespace WorldTimelineDebug
{
    public enum MapMarkerKind { Trail, Battle, BattleStory, Story, Waypoint, Treasure, Merchant, Ritual, Cleared, Companion }

    public static class WorldTimelineMapMarker
    {
        private const int Cell = 36;
        private static Texture2D atlas;
        private static Sprite[] sprites;
        private static readonly (string Label, Color32 Ink, string Glyph)[] styles =
        {
            ("Timeline_UI_93", new Color32(156, 52, 41, 255),
                "##........#/.##......##/..##....##./...##..##../....####.../.....##..../....####.../...##..##../.###....###/.##......##/#.........."),
            ("Timeline_UI_92", new Color32(156, 52, 41, 255),
                "#.....#####/##....#...#/.##...###.#/..##..#...#/...##.###.#/....##....#/.....######/.....###.../...###.##../...##....../..#........"),
            ("Timeline_UI_94", new Color32(61, 83, 120, 255),
                ".#######.../##.....##../#.......#../..#####.#../........#../..#####.#../........#../..###...#../#.......#../##.....##../.#######..."),
            ("Timeline_UI_98", new Color32(41, 105, 78, 255),
                "..........#/.......####/.....###..#/....##...#./...##...##./...#...##../...#.###.../...###...../..##......./.##......../##........."),
            ("Timeline_UI_96", new Color32(150, 97, 25, 255),
                ".....#...../....###..../...##.##.../..##...##../.##..#..##./##..###..##/.##..#..##./..##...##../...##.##.../....###..../.....#....."),
            ("Timeline_MerchantKind", new Color32(112, 75, 117, 255),
                "..#######../...##.##.../....###..../...#####.../..##...##../.##.....##./.#..###..#./.#..#.#..#./.#..###..#./.##.....##./..#######.."),
            ("Timeline_UI_91", new Color32(95, 46, 64, 255),
                "...#####.../..##...##../.##.....##./##...#...##/#...###...#/#..##.##..#/#...###...#/##...#...##/.##.....##./..##...##../...#####..."),
            ("Timeline_UI_95", new Color32(104, 105, 97, 255),
                ".........../.........../.........##/........##./.......##../......##.../##...##..../.##.##...../..###....../...#......./..........."),
            ("同伴关卡", new Color32(42, 115, 118, 255),
                "..##...##../.####.####./.####.####./..##...##../.........../.####.####./###########/###########/.####.####./.####.####./.##.#.#.##.")
        };

        public static int Count => styles.Length;
        public static string LabelKey(MapMarkerKind kind) =>
            kind == MapMarkerKind.Trail ? "Timeline_UI_98" : styles[(int)kind - 1].Label;

        internal static MapMarkerKind Classify(NodeState node, bool story, bool ritual, bool cleared)
        {
            if (ritual) return MapMarkerKind.Ritual;
            if (node.IsMerchant) return MapMarkerKind.Merchant;
            if (node.CompanionStageRole.HasValue) return MapMarkerKind.Companion;
            if (node.MonsterHealth > 0) return story ? MapMarkerKind.BattleStory : MapMarkerKind.Battle;
            if (story) return MapMarkerKind.Story;
            if (node.Items.Count > 0) return MapMarkerKind.Treasure;
            if (cleared) return MapMarkerKind.Cleared;
            return node.IsCultivationSite || node.CultivationPerYear >= 8 ? MapMarkerKind.Waypoint : MapMarkerKind.Trail;
        }

        internal static Texture2D Atlas
        {
            get
            {
                if (atlas == null) BuildAtlas();
                return atlas;
            }
        }

        public static Sprite GetSprite(MapMarkerKind kind)
        {
            var texture = Atlas;
            int index = (int)kind - 1;
            return sprites[index] ??= Sprite.Create(texture, new Rect(index * Cell, 0, Cell, Cell),
                new Vector2(.5f, .5f), Cell, 0, SpriteMeshType.FullRect);
        }

        internal static Vector4 AtlasRect(MapMarkerKind kind)
        {
            var texture = Atlas;
            return new Vector4((Cell - 1f) / texture.width, (Cell - 1f) / Cell,
                (((int)kind - 1) * Cell + .5f) / texture.width, .5f / Cell);
        }

        private static void BuildAtlas()
        {
            int width = Cell * styles.Length;
            var pixels = new Color32[width * Cell];
            Color paper = new Color32(251, 238, 205, 255);
            for (int index = 0; index < styles.Length; index++)
            {
                var style = styles[index];
                var glyph = style.Glyph.Split('/');
                for (int y = 0; y < Cell; y++)
                    for (int x = 0; x < Cell; x++)
                    {
                        float radius = new Vector2(x + .5f - Cell * .5f, y + .5f - Cell * .5f).magnitude;
                        Color color = Color.Lerp(style.Ink, paper, Mathf.InverseLerp(13.25f, 14.45f, radius));
                        color.a = 1f - Mathf.InverseLerp(16f, 17.45f, radius);
                        int gx = (x - 7) / 2, gy = (Cell - 1 - y - 7) / 2;
                        if (x >= 7 && x < 29 && y >= 7 && y < 29 && glyph[gy][gx] == '#')
                            color = paper;
                        pixels[y * width + index * Cell + x] = color;
                    }
            }
            atlas = new Texture2D(width, Cell, TextureFormat.RGBA32, mipChain: false)
            {
                name = "轮回关卡图标", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);
            sprites = new Sprite[styles.Length];
        }
    }
}
