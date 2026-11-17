using System;
using System.Linq;

namespace WorldTimeline
{
    [Serializable]
    public sealed class BalanceSample
    {
        public int schemaVersion = 1;
        public string runId, segmentId, configVersion, type, source, outcome, nodeName, eventName, battleId, segmentStartedUtc;
        public int seq, year, locationConfigId, q, r, qi, level, previousLevel, eventId, optionId;
        public int nodeId = -1;
        public int enemyHealth, enemyAttack, enemyCount, trialStage, hp, maxHp, attack, battleHp, enemyRemaining;
        public int healthCost, healing, costItemId, lootAttackBonus;
        public long threshold, experience, levelExperience, experienceGain, spiritStonesDelta, durationMs;
        public int[] items, techniqueIds, techniqueRanks, skillIds, usedSkillIds;
        public long[] thresholds;
    }
}
