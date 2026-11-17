using System;

namespace WorldTimeline
{
    [Serializable]
    public sealed class ChosenRouteStage
    {
        public int Q, R, TravelYears, GuardianCount, GuardianHealth, GuardianAttack, RitualPeriod;
        public string GuardianAppearance;

        public int NodeId => HexWorldMap.Id(Q, R);
    }
}
