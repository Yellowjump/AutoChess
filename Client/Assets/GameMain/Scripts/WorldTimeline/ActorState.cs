using System;
using System.Collections.Generic;

namespace WorldTimeline
{
    public sealed class ActorState
    {
        public int Id { get; }
        // Null while travelling: an actor cannot be encountered at the departure node.
        public int? NodeId { get; internal set; }
        public int? TravelOriginNodeId { get; internal set; }
        public ActorMapPoint? FrozenMapPoint { get; internal set; }
        public int Age { get; internal set; }
        public int Lifespan { get; internal set; }
        public int Level { get; internal set; } = 1;
        public long LevelExperience { get; internal set; }
        public TalentProfile Talents { get; internal set; }
        public int BaseHealth { get; internal set; }
        public int BaseAttack { get; internal set; }
        public int BaseLifespan { get; internal set; }
        public int Health { get; internal set; }
        public int Attack { get; internal set; }
        public int MaxHealth { get; internal set; }
        public int? LeaderId { get; internal set; }
        public bool HasJoinedChosen { get; internal set; }
        public bool IsDisguised { get; internal set; }
        public int DisguisedYear { get; internal set; }
        public long Experience { get; internal set; }
        public bool IsAlive => Health > 0;
        public WorldAction CurrentAction { get; internal set; }
        public int RemainingYears { get; internal set; }
        public List<int> Items { get; } = new List<int>();

        public ActorState(int id, int nodeId, int health = 100, int attack = 10, int lifespan = 100)
        {
            if (health < 1 || attack < 1 || lifespan < 1) throw new ArgumentOutOfRangeException();
            Id = id;
            NodeId = nodeId;
            Health = health;
            MaxHealth = health;
            Attack = attack;
            Lifespan = lifespan;
            Talents = TalentProfile.Generate(id);
            BaseHealth = health; BaseAttack = attack; BaseLifespan = lifespan;
        }

        internal void Die()
        {
            Health = 0;
            CurrentAction = null;
            RemainingYears = 0;
            NodeId = null;
            LeaderId = null;
            IsDisguised = false;
        }
    }
}
