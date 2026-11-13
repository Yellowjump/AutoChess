using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Video;

public sealed class OpeningShot : PlayableAsset
{
    [Serializable]
    public struct Caption
    {
        public string chinese;
        public string english;
        public float start;
        public float end;

        public string Text(bool englishLanguage) => englishLanguage ? english : chinese;
    }

    public Texture2D painting;
    public VideoClip motion;
    public Vector2 startFocus = new Vector2(.5f, .5f);
    public Vector2 endFocus = new Vector2(.5f, .5f);
    public float startZoom = 1f;
    public float endZoom = 1.08f;
    [Range(0, 1)] public float mist;
    [Min(0)] public float fadeIn;
    [Min(0)] public float fadeOut;
    public Caption heading;
    public Caption dialogue;
    public Caption secondDialogue;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) =>
        Playable.Create(graph);
}
