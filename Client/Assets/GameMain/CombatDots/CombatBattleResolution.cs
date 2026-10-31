namespace Zizouqi.CombatDots
{
    public static class CombatBattleResolutionBridge
    {
        // Call after the unit's Alive flag has been cleared. The board occupant
        // may already be in its next cell while the movement animation is running.
        public static bool ResolveUnitDeath(int unitHandle, out bool battleWon)
        {
            int heroUid = CombatAttributeBridge.GetHeroUid(unitHandle);
            int camp = CombatAttributeBridge.GetCamp(unitHandle);
            CombatBoardBridge.ClearOccupant(heroUid);
            battleWon = camp == 0;
            int updateGroup = camp == 1 ? 1 : 0;
            return CombatAttributeBridge.CountAliveInUpdateGroup(updateGroup) == 0;
        }
    }
}
