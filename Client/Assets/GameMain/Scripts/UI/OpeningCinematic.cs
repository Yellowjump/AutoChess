using System;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(PlayableDirector))]
public sealed class OpeningCinematic : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private AudioSource score;
    [SerializeField] private AudioSource atmosphere;
    [SerializeField] private RawImage image;
    [SerializeField] private RawImage mist;
    [SerializeField] private CanvasGroup picture;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private CanvasGroup subtitles;
    [SerializeField] private Button skip;
    [SerializeField] private TMP_Text skipLabel;
    [SerializeField] private Texture2D endingPaintings;
    [SerializeField] private AudioClip endingScore;
    [SerializeField] private AudioClip endingAtmosphere;

    private bool english;
    private Action completed;
    private VideoPlayer picturePlayer;
    private bool pictureStarted;
    private bool playbackStarted;
    private float prepareDeadline;
    private (string Title, string Body, string Art)[] ending;
    private int endingIndex;
    private string[] endingLines;
    private float[] endingLineEnds;
    private bool endingEnglish;
    private float endingStart;
    private float endingDuration;

    public bool TryPlay(bool useEnglish, float musicVolume, float effectsVolume, Action onComplete)
    {
        if (director.playableAsset == null || director.duration <= 0) return false;
        english = useEnglish;
        score.volume = Mathf.Clamp01(musicVolume);
        atmosphere.volume = Mathf.Clamp01(effectsVolume);
        skipLabel.text = english ? "Skip  /  Esc" : "跳过  /  Esc";
        completed = onComplete;
        skip.onClick.AddListener(Skip);
        director.stopped += OnStopped;
        PrepareMotion();
        prepareDeadline = Time.unscaledTime + 2f;
        if (picturePlayer == null) StartPlayback();
        return true;
    }

    public bool TryPlayEnding((string Title, string Body, string Art)[] pages, bool useEnglish,
        float musicVolume, float effectsVolume, Action onComplete)
    {
        if (endingPaintings == null || pages == null || pages.Length == 0) return false;
        ending = pages;
        endingEnglish = useEnglish;
        completed = onComplete;
        skipLabel.text = useEnglish ? "Skip  /  Esc" : "跳过  /  Esc";
        skip.onClick.AddListener(Skip);
        score.clip = endingScore;
        score.volume = Mathf.Clamp01(musicVolume * .70f);
        score.loop = true;
        if (score.clip != null) score.Play();
        atmosphere.clip = endingAtmosphere;
        atmosphere.volume = Mathf.Clamp01(effectsVolume * .50f);
        atmosphere.loop = false;
        if (atmosphere.clip != null) atmosphere.Play();
        BeginEndingShot();
        return true;
    }

    private void PrepareMotion()
    {
        if (!(director.playableAsset is TimelineAsset timeline)) return;
        foreach (var track in timeline.GetRootTracks())
        {
            if (!(track is OpeningShotTrack)) continue;
            foreach (var clip in track.GetClips())
            {
                if (!(clip.asset is OpeningShot shot) || shot.motion == null) continue;
                var player = gameObject.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.renderMode = VideoRenderMode.APIOnly;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                player.skipOnDrop = true;
                player.waitForFirstFrame = true;
                player.clip = shot.motion;
                picturePlayer = player;
                player.Prepare();
                return;
            }
        }
    }

    private void Update()
    {
        if (completed == null) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Skip();
            return;
        }
        if (ending != null)
        {
            UpdateEndingShot();
            return;
        }
        if (!playbackStarted && (picturePlayer.isPrepared || Time.unscaledTime >= prepareDeadline))
            StartPlayback();
    }

    private void StartPlayback()
    {
        playbackStarted = true;
        // Commit to video or paintings for the entire playback. Never flash a still into a running film.
        pictureStarted = picturePlayer != null && picturePlayer.isPrepared;
        if (pictureStarted) picturePlayer.Play();
        director.Play();
    }

    public void Cancel() => Close(stopDirector: true);

    private void Close(bool stopDirector)
    {
        completed = null;
        director.stopped -= OnStopped;
        if (stopDirector) director.Stop();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void BeginEndingShot()
    {
        var page = ending[endingIndex];
        heading.text = page.Title;
        endingLines = page.Body.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (endingLines.Length == 0) endingLines = new[] { "" };
        endingDuration = .45f;
        endingLineEnds = new float[endingLines.Length];
        for (int i = 0; i < endingLines.Length; i++)
        {
            endingDuration += Mathf.Max(3.2f, endingLines[i].Length / (endingEnglish ? 21f : 8.5f) + .6f);
            endingLineEnds[i] = endingDuration;
        }
        endingDuration += .55f;
        endingStart = Time.unscaledTime;
        DrawEndingShot(0);
    }

    private void UpdateEndingShot()
    {
        float elapsed = Time.unscaledTime - endingStart;
        if (elapsed < endingDuration)
        {
            DrawEndingShot(elapsed);
            return;
        }
        if (++endingIndex == ending.Length) Finish(stopDirector: false);
        else BeginEndingShot();
    }

    private void DrawEndingShot(float elapsed)
    {
        int tile = ending[endingIndex].Art switch
        {
            "Deed" => 1, "Spring" => 2, "Sluice" => 3, "Book" => 4, "Road" => 5, _ => 0
        };
        float progress = Mathf.SmoothStep(0f, 1f, elapsed / endingDuration);
        var rect = image.rectTransform.rect;
        float viewAspect = rect.width / Mathf.Max(1f, rect.height);
        float tileAspect = (endingPaintings.width / 2f) / (endingPaintings.height / 3f);
        float zoom = Mathf.Lerp(1.04f, 1.14f, progress);
        float width = Mathf.Min(.98f, viewAspect / tileAspect) / zoom;
        float height = Mathf.Min(.98f, tileAspect / Mathf.Max(.01f, viewAspect)) / zoom;
        image.texture = endingPaintings;
        image.uvRect = new Rect((tile % 2 + (1f - width) * Mathf.Lerp(.40f, .60f, progress)) / 2f,
            (2 - tile / 2 + (1f - height) * Mathf.Lerp(.40f, .55f, progress)) / 3f,
            width / 2f, height / 3f);
        picture.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(elapsed, endingDuration - elapsed) / .52f));
        mist.color = new Color(.84f, .84f, .79f, .17f);
        mist.uvRect = new Rect(.04f + .15f * progress, .05f, .8f, .9f);
        heading.alpha = picture.alpha * Mathf.Clamp01((2.6f - elapsed) / .55f);

        int line = 0;
        while (line < endingLines.Length - 1 && elapsed >= endingLineEnds[line]) line++;
        float start = line == 0 ? .45f : endingLineEnds[line - 1];
        if (subtitle.text != endingLines[line]) subtitle.text = endingLines[line];
        subtitles.alpha = picture.alpha * Mathf.Clamp01(Mathf.Min(elapsed - start, endingLineEnds[line] - elapsed) / .24f);
    }

    private void OnStopped(PlayableDirector _) => Finish(stopDirector: false);
    private void Skip() => Finish(stopDirector: true);

    private void Finish(bool stopDirector)
    {
        var callback = completed;
        if (callback == null) return;
        Close(stopDirector);
        callback();
    }

    public void ClearShot()
    {
        picture.alpha = 0;
        subtitles.alpha = 0;
        heading.alpha = 0;
    }

    public void ShowShot(OpeningShot shot, float time, float duration)
    {
        if (shot.painting == null)
        {
            ClearShot();
            return;
        }

        var motion = pictureStarted ? picturePlayer.texture : null;
        if (pictureStarted && (picturePlayer.frame < 0 || motion == null))
        {
            return;
        }
        var source = motion == null ? shot.painting : motion;
        float t = Mathf.SmoothStep(0, 1, time / duration);
        var rect = image.rectTransform.rect;
        float viewportAspect = rect.width / Mathf.Max(1, rect.height);
        float artAspect = (float)source.width / source.height;
        float zoom = motion == null ? Mathf.Max(1, Mathf.Lerp(shot.startZoom, shot.endZoom, t)) : 1;
        float width = Mathf.Min(1, viewportAspect / artAspect) / zoom;
        float height = Mathf.Min(1, artAspect / Mathf.Max(.01f, viewportAspect)) / zoom;
        Vector2 center = motion == null ? Vector2.Lerp(shot.startFocus, shot.endFocus, t) : new Vector2(.5f, .5f);
        image.texture = source;
        image.uvRect = new Rect(Mathf.Clamp(center.x - width / 2, 0, 1 - width),
            Mathf.Clamp(center.y - height / 2, 0, 1 - height), width, height);

        float fadeIn = shot.fadeIn > 0 ? Mathf.Clamp01(time / shot.fadeIn) : 1;
        float fadeOut = shot.fadeOut > 0 ? Mathf.Clamp01((duration - time) / shot.fadeOut) : 1;
        picture.alpha = Mathf.SmoothStep(0, 1, fadeIn * fadeOut);
        mist.color = new Color(.84f, .84f, .79f, motion == null ? shot.mist : .22f);
        float mistTime = motion == null ? t : (float)(director.time / director.duration);
        mist.uvRect = new Rect(.04f + .15f * mistTime, .05f, .8f, .9f);

        heading.alpha = SetCaption(heading, shot.heading, time);
        var line = time >= shot.secondDialogue.start && shot.secondDialogue.end > shot.secondDialogue.start
            ? shot.secondDialogue : shot.dialogue;
        subtitles.alpha = SetCaption(subtitle, line, time);
    }

    private float SetCaption(TMP_Text label, OpeningShot.Caption caption, float time)
    {
        string value = caption.Text(english);
        float alpha = string.IsNullOrEmpty(value) ? 0 :
            Mathf.Clamp01(Mathf.Min(time - caption.start, caption.end - time) / .18f);
        if (alpha > 0 && label.text != value) label.text = value;
        return alpha;
    }
}
