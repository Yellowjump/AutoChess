namespace WorldTimeline
{
    public static class SaltChannel
    {
        public const int FirstLocationId = 72, CrabLocationId = 73, BeastLocationId = 75, LastLocationId = 75;
        public const int CrabSkillId = 9208, BeastSkillId = 9209, RewardItemId = 9180;
        public const string CrabAppearance = "SaltShellCrab", BeastAppearance = "BronzeSluiceBeast";
        public const string DrainedFlag = "salt_channel_drained";
        public static readonly int Spring = HexWorldMap.Id(8, -8), Kiln = HexWorldMap.Id(6, -8),
            Ditch = HexWorldMap.Id(8, -9), Beach = HexWorldMap.Id(7, -8),
            Drain = HexWorldMap.Id(6, -7), Head = HexWorldMap.Id(6, -6);
    }
}
