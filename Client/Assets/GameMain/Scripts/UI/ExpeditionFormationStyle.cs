using TMPro;
using UnityEngine;

// References existing project art so the formation skin also ships in player builds.
public sealed class ExpeditionFormationStyle : ScriptableObject
{
    public Texture2D PaperTexture;
    public Texture2D Landscape;
    public Sprite PaperFrame;
    public Sprite Seal;
    public Sprite[] Portraits;
    private static TMP_FontAsset titleFont;

    public TMP_FontAsset HeadingFont(TMP_FontAsset fallback)
    {
        if (titleFont != null) return titleFont;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        string path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts), "simkai.ttf");
        if (System.IO.File.Exists(path)) titleFont = TMP_FontAsset.CreateFontAsset(path, 0, 72, 8,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
#endif
        if (titleFont == null) return fallback;
        titleFont.isMultiAtlasTexturesEnabled = true;
        titleFont.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
        titleFont.fallbackFontAssetTable.Add(fallback);
        return titleFont;
    }

    public Sprite Portrait(int index) => Portraits != null && index >= 0 && index < Portraits.Length ? Portraits[index] : null;
}
