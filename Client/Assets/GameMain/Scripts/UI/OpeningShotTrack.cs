using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackClipType(typeof(OpeningShot))]
[TrackBindingType(typeof(OpeningCinematic))]
public sealed class OpeningShotTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var mixer = ScriptPlayable<Mixer>.Create(graph, inputCount);
        mixer.GetBehaviour().Clips = GetClips().ToArray();
        return mixer;
    }

    private sealed class Mixer : PlayableBehaviour
    {
        public TimelineClip[] Clips;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!(playerData is OpeningCinematic view)) return;
            double time = playable.GetTime();
            foreach (var clip in Clips)
            {
                if (time < clip.start || time >= clip.end) continue;
                view.ShowShot((OpeningShot)clip.asset, (float)(time - clip.start), (float)clip.duration);
                return;
            }
            view.ClearShot();
        }
    }
}
