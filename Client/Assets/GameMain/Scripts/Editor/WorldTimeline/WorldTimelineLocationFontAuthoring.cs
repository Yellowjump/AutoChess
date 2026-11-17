using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class WorldTimelineLocationFontAuthoring
{
    private const string FontDirectory = "Assets/GameMain/Fonts/";
    private const string FontPath = FontDirectory + "WorldTimelineLocation SDF.asset";
    private const string TableDirectory = "Assets/GameMain/Data/DataTables/";

    private static readonly HashSet<string> DetailKeys = new HashSet<string>(new[]
    {
        "Timeline_Opening_Gate", "Timeline_QiStory_Pass",
        "Timeline_NorthRelay_Spring", "Timeline_NorthRelay_WreckBeastPass",
        "Timeline_NorthRelay_SpringDry", "Timeline_NorthRelay_SpringFlowing",
        "Timeline_SwordTrial_Name", "Timeline_SwordTrial_Guardians", "Timeline_Sluice_DrainedBossHint",
        "Timeline_MerchantKind", "Timeline_MerchantHint",
        "Timeline_ExplorerRelic_0", "Timeline_ExplorerRelic_1", "Timeline_ExplorerRelic_2"
    }.Concat(new[]
    {
        "14", "25", "28", "29", "37", "51", "76", "79", "86", "88", "89", "90", "91", "92", "93",
        "94", "95", "96", "97", "98", "99", "100", "101", "102", "103", "104", "105",
        "PastLife", "LootAttack", "RelicNumber"
    }.Select(key => "Timeline_UI_" + key)), StringComparer.Ordinal);

    private static readonly string[] DetailPrefixes =
    {
        "Timeline_Arrival_", "Timeline_Merchant_"
    };

    [MenuItem("Tools/World Timeline/Bake Location Details Font")]
    public static void Bake()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play mode before baking the location font.");

        string characters = CollectCharacters();
        var source = AssetDatabase.LoadAssetAtPath<Font>(FontDirectory + "SIMHEI.TTF");
        if (source == null)
            throw new InvalidOperationException("Missing SIMHEI.TTF.");

        // Use one page so the prefab can share the same font material for every detail label.
        var baked = TMP_FontAsset.CreateFontAsset(source, 56, 7, GlyphRenderMode.SDFAA,
            2048, 2048, AtlasPopulationMode.Dynamic, false);
        var atlas = baked.atlasTexture;
        var material = baked.material;
        try
        {
            if (!baked.TryAddCharacters(characters, out string missing) || !baked.HasCharacters(characters))
                throw new InvalidOperationException("Could not bake all location characters. Missing: " + missing);

            baked.atlasPopulationMode = AtlasPopulationMode.Static;
            baked.name = "WorldTimelineLocation SDF";
            atlas.name = "WorldTimelineLocation Atlas";
            material.name = "WorldTimelineLocation Material";

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(baked, FontPath);
                AssetDatabase.AddObjectToAsset(atlas, baked);
                AssetDatabase.AddObjectToAsset(material, baked);
            }
            else
            {
                // Keep the asset and sub-asset IDs referenced by the prefab.
                var existingAtlas = existing.atlasTexture;
                var existingMaterial = existing.material;
                EditorUtility.CopySerialized(atlas, existingAtlas);
                material.mainTexture = existingAtlas;
                EditorUtility.CopySerialized(material, existingMaterial);
                baked.atlasTextures = new[] { existingAtlas };
                baked.material = existingMaterial;
                EditorUtility.CopySerialized(baked, existing);
                EditorUtility.SetDirty(existingAtlas);
                EditorUtility.SetDirty(existingMaterial);
                EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceSynchronousImport);
            var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            saved.ReadFontAssetDefinition();
            if (saved.atlasPopulationMode != AtlasPopulationMode.Static || !saved.HasCharacters(characters))
                throw new InvalidOperationException("The saved location font failed glyph coverage validation.");

            Debug.Log($"Baked and verified {characters.Length} location characters in {FontPath}.");
        }
        finally
        {
            foreach (var obj in new UnityEngine.Object[] { baked, material, atlas })
                if (obj != null && !EditorUtility.IsPersistent(obj))
                    UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    private static string CollectCharacters()
    {
        // ASCII covers coordinates, numbers and localization-key fallbacks. These literals
        // also cover the separator and the initial text stored in LocationDetails.
        const string literals = "—、战斗关卡古修剑谱攻击";
        var translations = ReadTable("Language").Skip(4)
            .Where(row => row.Length >= 6 && row[0] != "#" && row[3].Length > 0)
            .ToLookup(row => row[3], row => row.Skip(4).Take(2), StringComparer.Ordinal);
        var configuredValues = ReadColumns("OuterLocations", "Name", "LootName")
            .Concat(ReadColumns("StartingCultivators", "Name"))
            .Concat(ReadColumns("StoryEvent", "Title", "Text"))
            .Concat(ReadColumns("StoryItem", "Name"));
        var keys = DetailKeys.Concat(translations.Select(row => row.Key)
            .Where(key => DetailPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal))))
            .Concat(configuredValues).Distinct(StringComparer.Ordinal);
        var text = keys.SelectMany(key => translations[key].SelectMany(values => values).DefaultIfEmpty(key))
            .SelectMany(value => value);
        var characters = Enumerable.Range(32, 95).Select(code => (char)code)
            .Concat(literals).Concat(text).Where(character => !char.IsControl(character))
            .Distinct().OrderBy(character => character);
        return new string(characters.ToArray());
    }

    private static string[][] ReadTable(string table) =>
        File.ReadLines(TableDirectory + table + ".txt").Select(line => line.Split('\t')).ToArray();

    private static IEnumerable<string> ReadColumns(string table, params string[] columns)
    {
        var rows = ReadTable(table);
        var indices = columns.Select(column =>
        {
            int index = Array.IndexOf(rows[1], column);
            if (index < 0)
                throw new InvalidOperationException($"Column {column} is missing from {table}.");
            return index;
        }).ToArray();

        return rows.Skip(4).Where(row => row[0] != "#")
            .SelectMany(row => indices.Where(index => index < row.Length).Select(index => row[index]))
            .Where(value => value.Length > 0);
    }
}
