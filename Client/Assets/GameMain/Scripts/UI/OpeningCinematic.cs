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

    private bool english;
    private Action completed;
    private VideoPlayer picturePlayer;
    private bool pictureStarted;
    private bool playbackStarted;
    private float prepareDeadline;

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
