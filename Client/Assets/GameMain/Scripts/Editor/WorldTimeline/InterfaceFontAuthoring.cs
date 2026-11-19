using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class InterfaceFontAuthoring
{
    private const string Root = "Assets/GameMain/";
    private const string Output = Root + "Prefeb/Resources/";

    [MenuItem("Tools/World Timeline/Bake Interface Fonts")]
    public static void Bake()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before baking.");
        var characters = new SortedSet<char>(Enumerable.Range(32, 95).Select(i => (char)i));
        characters.UnionWith("…□\u3000");
        foreach (var path in Directory.GetFiles(Root + "Data/DataTables", "*.txt")
            .Concat(Directory.GetFiles(Root + "Data/Resources", "*.csv"))
            .Concat(Directory.GetFiles(Root + "Scripts/UI", "*.cs"))
            .Concat(Directory.GetFiles(Root + "Scripts/WorldTimeline", "*.cs", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(Root + "Scripts/WorldTimelineDebug", "*.cs")))
            characters.UnionWith(File.ReadAllText(path).Where(c => !char.IsControl(c) && !char.IsWhiteSpace(c)));

        foreach (string panel in new[] { "main_title_panel", "confirm_panel", "world_timeline_panel", "world_timeline_battle_report" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefeb/UIPrefab/" + panel + ".prefab");
            foreach (var label in prefab.GetComponentsInChildren<TMP_Text>(true))
                characters.UnionWith(label.text.Where(c => !char.IsControl(c)));
        }
        characters.Remove('\uFEFF');
        string corpus = new string(characters.ToArray());
        var source = AssetDatabase.LoadAssetAtPath<Font>(Root + "Fonts/SIMHEI.TTF");
        var body = TMP_FontAsset.CreateFontAsset(source, 40, 4, GlyphRenderMode.SDFAA, 2048, 2048,
            AtlasPopulationMode.Dynamic, false);
        var symbolSource = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        var symbols = TMP_FontAsset.CreateFontAsset(symbolSource, 40, 4, GlyphRenderMode.SDFAA, 128, 128,
            AtlasPopulationMode.Dynamic, false);
        symbols = WorldTimelineLocationFontAuthoring.SaveStaticFont(symbols, "•−", Output + "InterfaceSymbols.asset");
        var savedBody = WorldTimelineLocationFontAuthoring.SaveStaticFont(body, corpus, Output + "InterfaceBody.asset", symbols);
        BakePaper("simkai.ttf", "InterfaceKai", corpus, 2048, savedBody);
        BakePaper("georgia.ttf", "InterfaceGeorgia", new string(characters.Where(c => c < 127).ToArray()) + "…·—", 1024, savedBody);
    }

    private static void BakePaper(string file, string name, string characters, int size, TMP_FontAsset fallback)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), file);
        if (!File.Exists(path)) throw new FileNotFoundException("Missing font for baking", path);
        var font = TMP_FontAsset.CreateFontAsset(path, 0, 40, 4, GlyphRenderMode.SDFAA, size, size);
        font.isMultiAtlasTexturesEnabled = false;
        WorldTimelineLocationFontAuthoring.SaveStaticFont(font, characters, Output + name + ".asset", fallback);
    }
}
