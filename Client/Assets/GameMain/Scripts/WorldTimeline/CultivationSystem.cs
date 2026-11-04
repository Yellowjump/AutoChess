using System;

namespace WorldTimeline
{
    public static class CultivationSystem
    {
        public static void Initialize(ActorState actor, CultivationConfig config, TalentProfile talents, int level = 1)
        {
            if (actor.Age != 0 || actor.CurrentAction != null || actor.Experience != 0 || !actor.IsAlive)
                throw new InvalidOperationException("Growth initialization is only allowed at birth.");
            config.GetLevel(level);
            actor.Talents = talents ?? throw new ArgumentNullException(nameof(talents));
            actor.Level = level;
            int health = 0, attack = 0, lifespan = 0;
            for (int i = 1; i <= level; i++)
            {
                var entry = config.GetLevel(i);
                health = checked(health + entry.Range(GrowthAttribute.Health, talents.Health.Grade).SampleAtLevel(talents.Health, i));
                attack = checked(attack + entry.Range(GrowthAttribute.Attack, talents.Attack.Grade).SampleAtLevel(talents.Attack, i));
                lifespan = checked(lifespan + entry.Range(GrowthAttribute.Lifespan, talents.Lifespan.Grade).SampleAtLevel(talents.Lifespan, i));
            }
            actor.Health = actor.MaxHealth = actor.BaseHealth = health;
            actor.Attack = actor.BaseAttack = attack;
            actor.Lifespan = actor.BaseLifespan = lifespan;
            actor.LevelExperience = 0;
        }

        internal static void Gain(ActorState actor, int experience, CultivationConfig config, int year, Action<WorldEvent> emit)
        {
            if (!actor.IsAlive || actor.HasJoinedChosen) return;
            if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
            actor.Experience = checked(actor.Experience + experience);
            if (config == null) return;
            actor.LevelExperience = checked(actor.LevelExperience + experience);
            while (actor.Level < config.Levels.Count && actor.LevelExperience >= config.GetLevel(actor.Level).ExperienceToNext)
            {
                actor.LevelExperience -= config.GetLevel(actor.Level).ExperienceToNext;
                var next = config.GetLevel(actor.Level + 1);
                int health = checked(actor.BaseHealth + next.Range(GrowthAttribute.Health, actor.Talents.Health.Grade).SampleAtLevel(actor.Talents.Health, next.Level));
                int attack = checked(actor.BaseAttack + next.Range(GrowthAttribute.Attack, actor.Talents.Attack.Grade).SampleAtLevel(actor.Talents.Attack, next.Level));
                int lifespan = checked(actor.BaseLifespan + next.Range(GrowthAttribute.Lifespan, actor.Talents.Lifespan.Grade).SampleAtLevel(actor.Talents.Lifespan, next.Level));
                // Keep missing health and any external bonuses; a breakthrough is not a full heal.
                actor.Health = checked(actor.Health + health - actor.BaseHealth);
                actor.MaxHealth = checked(actor.MaxHealth + health - actor.BaseHealth);
                actor.Attack = checked(actor.Attack + attack - actor.BaseAttack);
                actor.Lifespan = checked(actor.Lifespan + lifespan - actor.BaseLifespan);
                actor.BaseHealth = health; actor.BaseAttack = attack; actor.BaseLifespan = lifespan;
                actor.Level++;
                emit(new WorldEvent(year, WorldEventKind.Breakthrough, actor.Id, actor.NodeId, "Level " + actor.Level));
            }
        }
    }
}
