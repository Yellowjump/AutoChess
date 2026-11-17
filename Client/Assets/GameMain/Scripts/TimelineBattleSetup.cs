using System;
using System.Collections.Generic;
using Entity;
using UnityEngine;
using WorldTimeline;

// Optional encounter setup shared by the visible battle and its deterministic simulation.
internal sealed class TimelineBattleSetup
{
    public Func<ActorState, Vector2Int> Position;
    public Func<ActorState, Vector2Int, Vector2Int> AdjustPosition;
    public Action<ActorState, EntityQizi> Configure;
    public Action<float> Tick;
    public float? DurationSeconds;
    public bool ExpeditionView;
    public Func<IReadOnlyList<ActorState>, IReadOnlyList<EntityQizi>, System.Collections.IEnumerator> Planning;
}
