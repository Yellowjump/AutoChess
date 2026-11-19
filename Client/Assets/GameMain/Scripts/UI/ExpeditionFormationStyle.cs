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
    public TMP_FontAsset TitleFont;

    public TMP_FontAsset HeadingFont(TMP_FontAsset fallback) => TitleFont != null ? TitleFont : fallback;

    public Sprite Portrait(int index) => Portraits != null && index >= 0 && index < Portraits.Length ? Portraits[index] : null;
}

public static class ExpeditionRosterPortraits
{
    public static Sprite PastPortrait(this WorldTimelineDebug.WorldTimelineDebugView view, WorldTimeline.ExpeditionParty party, int role)
    {
        int index = view.PastPortraitIndex(party, role);
        return index < 0 ? null : Resources.Load<ExpeditionFormationStyle>("ExpeditionFormationStyle")?.Portrait(index);
    }
}
