using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WorldTimeline
{
    [Flags]
    public enum ArtifactTrigger { None = 0, ShieldOpening = 1, FrontOpening = 2, Pursuit = 4, Remedy = 8, Recovery = 16, SwordCase = 32, MountainRing = 64, SpringPendant = 128, Bamboo = 256, Sand = 512, Tide = 1024, OpeningSword = 2048, Quake = 4096, RainTassel = 8192, RelayClasp = 16384, ReprisalSeal = 32768, HeartLamp = 65536, TinderPouch = 131072, MeridianSpike = 262144, ThunderBell = 524288, FalterMirror = 1048576, LastStandCharm = 2097152, AfflictionSeal = 4194304, EchoJade = 8388608, ComradeKnot = 16777216, FieldKit = 33554432, HiddenEdgePearl = 1 << 26, JadeRendBlade = 1 << 27, BreachMirror = 1 << 28 }

    public static class MeridianBleed
    {
        public static bool TryConsume<T>(List<T> layers, int durationMs, Func<T,int> paidTicks,
            Func<T,int> damagePerTick, out long damage)
        {
            damage = 0;
            if (layers == null || layers.Count < 3) return false;
            for (int i = 0; i < 2; i++)
            {
                var layer = layers[i];
                int remainingTicks = Math.Max(0, durationMs / 1000 - paidTicks(layer));
                damage += (long)remainingTicks * damagePerTick(layer);
            }
            layers.RemoveRange(0, 2);
            return true;
        }
    }

    public enum PursuitHit { None, Opening, Relay }

    public sealed class PursuitMark
    {
        private float openingUntil, relayUntil;
        private int relayStarter;

        public void Apply(float now) => openingUntil = now + 4f;

        public static int BonusDamage(int attack) => Math.Max(1, attack / 2);

        public PursuitHit OnBasicHit(float now, int attackerId, ArtifactTrigger effects)
        {
            if (now < relayUntil)
            {
                if (attackerId == relayStarter) return PursuitHit.None;
                relayUntil = 0;
                return PursuitHit.Relay;
            }
            if (now >= openingUntil || (effects & ArtifactTrigger.Pursuit) == 0) return PursuitHit.None;

            openingUntil = 0;
            if ((effects & ArtifactTrigger.RelayClasp) != 0)
            {
                relayStarter = attackerId;
                relayUntil = now + 2f;
            }
            return PursuitHit.Opening;
        }
    }

    public sealed class ArtifactVitality
    {
        private long sealHealthLost;
        private long ringLoss;
        private int ringUses;
        private bool pendantArmed, pendantUsed;
        private int repeatAmount;
        private float repeatAt;

        public int LoseHealth(ArtifactTrigger effects,int before,int after,int maximum,int attack)
        {
            if (after <= 0 || after >= before) return 0;
            if ((effects & ArtifactTrigger.ReprisalSeal) != 0)
                sealHealthLost = Math.Min(2L * Math.Max(0, attack), sealHealthLost + before - after);
            if ((effects & ArtifactTrigger.SpringPendant) != 0 && after*2L < maximum)
                pendantArmed = true;
            if ((effects & ArtifactTrigger.MountainRing) == 0 || ringUses == 2) return 0;
            int threshold = ExpeditionEquipment.RingThreshold(maximum);
            ringLoss += before-after;
            int procs = (int)Math.Min(2-ringUses,ringLoss/threshold);
            ringLoss -= (long)procs*threshold;
            ringUses += procs;
            return procs*Math.Max(1,(int)(8L*maximum/100));
        }

        public int SpendSealCharge(int attack)
        {
            int damage = (int)Math.Min(Math.Max(0, attack), sealHealthLost / 2);
            sealHealthLost = 0;
            return damage;
        }

        public void OnRemedy(int amount,float now)
        {
            if (!pendantArmed || pendantUsed) return;
            pendantUsed = true;
            repeatAmount = amount;
            repeatAt = now+2f;
        }

        public int TakeRepeat(float now)
        {
            if (now < repeatAt) return 0;
            int amount = repeatAmount;
            repeatAmount = 0;
            return amount;
        }
    }

    public sealed class ArtifactDefinition
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public bool Innate { get; internal set; }
        public int AutoSkillItem => Id switch { ExpeditionEquipment.StormCloud => 9210, ExpeditionEquipment.SunMirror => 9211, ExpeditionEquipment.EmberStamp => 9212, ExpeditionEquipment.TideChime => 9213, _ => 0 };
        public int BattleSkillItem => Id == ExpeditionEquipment.SpiritSpindle ? 9214 : AutoSkillItem;
        public bool Personal => Id == ExpeditionEquipment.OpeningBrush || Trigger is ArtifactTrigger.Bamboo or ArtifactTrigger.Sand or ArtifactTrigger.Tide;
        public int Icon { get; internal set; }
        public int Health { get; internal set; }
        public int Attack { get; internal set; }
        public int HealthStep { get; internal set; }
        public int AttackStep { get; internal set; }
        public int MaxLevel { get; internal set; }
        public ArtifactTrigger Trigger { get; internal set; }
        public string TriggerName => AutoSkillItem != 0 ? "自动" : Id == ExpeditionEquipment.SpiritSpindle ? "自动道具增伤" : Id == ExpeditionEquipment.OpeningBrush ? "命中被动" : Trigger switch
        {
            ArtifactTrigger.ShieldOpening or ArtifactTrigger.FrontOpening => "反击标记",
            ArtifactTrigger.SwordCase => "藏锋", ArtifactTrigger.MountainRing => "承岳", ArtifactTrigger.SpringPendant => "留春",
            ArtifactTrigger.EchoJade => "招式回响",
            ArtifactTrigger.ComradeKnot => "同列护援",
            ArtifactTrigger.FieldKit => "首通行医",
            ArtifactTrigger.HiddenEdgePearl => "普攻蓄暴",
            ArtifactTrigger.JadeRendBlade => "暴击伤害",
            ArtifactTrigger.BreachMirror => "队友必暴",
            ArtifactTrigger.AfflictionSeal => "三煞合击",
            ArtifactTrigger.LastStandCharm => "背水",
            ArtifactTrigger.FalterMirror => "折势",
            ArtifactTrigger.ThunderBell => "借印跳雷",
            ArtifactTrigger.MeridianSpike => "截脉",
            ArtifactTrigger.TinderPouch => "点火",
            ArtifactTrigger.HeartLamp => "同心传疗",
            ArtifactTrigger.ReprisalSeal => "偿锋",
            ArtifactTrigger.RelayClasp => "接力追击",
            ArtifactTrigger.RainTassel => "听雨回剑", ArtifactTrigger.Quake => "震击", ArtifactTrigger.OpeningSword => "养剑", ArtifactTrigger.Bamboo => "青竹剑阵", ArtifactTrigger.Sand => "镇砂护阵", ArtifactTrigger.Tide => "听潮护生",
            ArtifactTrigger.Pursuit => "追击", ArtifactTrigger.Remedy => "回春", ArtifactTrigger.Recovery => "养血", _ => ""
        };
        public string TriggerRule => Id == ExpeditionEquipment.StormCloud ? "在最近敌人所在位置上空召来雷云，持续6秒。每2秒雷击云下最近的一名敌人，造成80%攻击力伤害。冷却10秒。"
            : Id == ExpeditionEquipment.SunMirror ? "肩侧升起八瓣古镜，蓄光0.8秒后射出长8格的金光，持续0.4秒。每名敌人每次施放最多受到一次120%攻击力伤害。冷却8秒。"
            : Id == ExpeditionEquipment.EmberStamp ? "锁定6格内最近敌人的位置，0.65秒后落下赤焰印，对落点1.6格内所有敌人造成110%攻击力伤害。冷却7秒。"
            : Id == ExpeditionEquipment.TideChime ? "3格内出现敌人时鸣响两次，间隔0.9秒。每响对佩戴者周围3格内所有敌人造成45%攻击力伤害。冷却6秒。"
            : Id == ExpeditionEquipment.SpiritSpindle ? "佩戴者的自动道具技能伤害提高25%。同名效果只生效一次。"
            : Id == ExpeditionEquipment.OpeningBrush
            ? "祁墨以点墨普攻。命中存活的敌人时施加4秒虚弱，使其直接伤害降低15%，并积累1层破绽，最多3层。" : Trigger switch
        {
            ArtifactTrigger.EchoJade => "普攻命中2次后，下次施放的主动伤害招式获得回响，对首个命中后仍存活的敌人追加自身攻击力80%的伤害。每次施法最多一次。",
            ArtifactTrigger.ComradeKnot => "佩戴者在后排时，正前方同列队友首次受伤降到半血或以下且仍存活，获得佩戴者气血上限12%的护盾，持续4秒。每战一次。",
            ArtifactTrigger.HiddenEdgePearl => "每次普攻击中且未暴击，下次普攻的暴击率提高10个百分点，最多100%。普攻暴击后清空，每场重新积累。",
            ArtifactTrigger.JadeRendBlade => "佩戴者的暴击伤害提高40个百分点。相同法器的特殊效果只生效一次。",
            ArtifactTrigger.BreachMirror => "普攻暴击使存活目标获得3秒死隙。另一名队友的主动伤害招式命中后消耗死隙，此次招式对该目标必暴。每人8秒一次，每个目标最多一个死隙。",
            ArtifactTrigger.FieldKit => "佩戴者上阵并存活完成战斗首通后，为气血比例最低的存活队员恢复其气血上限的12%，候补也可受疗。每关全队只触发一次。",
            ArtifactTrigger.AfflictionSeal => "普攻命中仍存活的目标时，按命中前已有的燃烧、流血、虚弱种类数结算。任意两种追加自身攻击力50%的伤害，三种追加100%。同种只计一次，保留所有异常。",
            ArtifactTrigger.LastStandCharm => "每战一次：入场或受伤后，若存活且气血不高于一半，获得3次强化。此后普攻命中仍存活的敌人，消耗1次，追加自身攻击力60%的伤害（最低1点）。回血保留强化，本场结束时清空。",
            ArtifactTrigger.FalterMirror => "佩戴者站在前排，受到敌方直接伤害后若仍存活，使攻击者虚弱4秒，直接伤害降低15%。若攻击者已虚弱，刷新虚弱并叠1层破绽，最多3层。护盾抵挡也可触发，每人每3秒一次。",
            ArtifactTrigger.ThunderBell => "每第3次普攻命中存活敌人，向目标周围2.5单位内最近的另一名敌人跳雷一次，造成自身攻击力60%的伤害。若主目标带有我方雷印，每次普攻击中都可跳雷，保留雷印。",
            ArtifactTrigger.MeridianSpike => "普攻击中存活敌人后，若其流血不足3层，施加1层流血。已有3层或更多时，消耗最早的2层，立即结算这两层尚未造成的全部伤害。",
            ArtifactTrigger.TinderPouch => "每场战斗首次普攻命中并留下存活目标时，施加2层燃烧。此后每再命中3次存活敌人，给当次目标施加1层燃烧。换目标保留计数。",
            ArtifactTrigger.HeartLamp => "佩戴者为其他队员恢复气血时，将实际恢复量的50%传给另一名气血比例最低的存活伤员，向下取整。可传给佩戴者，每次治疗只传一次。",
            ArtifactTrigger.ReprisalSeal => "每实际损失2点气血，为下一次普攻积攒1点追加伤害，上限为自身攻击力。替队友分担的气血损失也计入。普攻击中后清空蓄力，每场战斗从零开始。",
            ArtifactTrigger.RelayClasp => "获得追击。普攻消耗反击标记时，追加自身攻击力50%的伤害。随后2秒内，另一名队友普攻命中同一敌人，可再追加该队友攻击力50%的伤害。每个标记只可接力一次。",
            ArtifactTrigger.RainTassel => "敌方直接伤害击破自身护盾时，向破盾者放出一柄飞剑，命中造成自身攻击力100%的伤害，受青竹剑和归鞘影响。闻潮在场且佩戴者在桑宁治疗范围内时，桑宁开战先治疗一次。优先救治伤员，范围内全员满血时优先为佩戴者积攒护盾。",
            ArtifactTrigger.Quake => "敌方直接伤害击破自身护盾时，对周围一格内每名敌人造成自身攻击力100%的伤害。",
            ArtifactTrigger.OpeningSword => "佩戴者上阵，随队首通两处守关后养成。此后每场开战放出一柄飞剑，命中造成自身攻击力的伤害，可归鞘。",
            ArtifactTrigger.Bamboo => "青篁上阵时，全队每轮飞剑额外生成一柄，继承归鞘。",
            ArtifactTrigger.Sand => "玄砂上阵时，替其他队员承担盾后伤害的15%，直接扣除自身气血。",
            ArtifactTrigger.Tide => "闻潮上阵时，友方溢出治疗的50%转为4秒护盾，上限为受疗者气血上限的10%。",
            ArtifactTrigger.SwordCase => "每4次普攻命中放出一柄飞剑，每次命中造成自身攻击力的伤害，可归鞘。",
            ArtifactTrigger.MountainRing => "每累计损失15%最大气血，获得8%最大气血的护盾，持续4秒，每战两次。",
            ArtifactTrigger.SpringPendant => "本场首次跌破半血后，下一次装备回春在2秒后重复一次，每战一次。",
            ArtifactTrigger.ShieldOpening => "护盾挡住直接伤害后，标记攻击者4秒。每人3秒一次。",
            ArtifactTrigger.FrontOpening => "前排受到直接伤害后，标记攻击者4秒。每人3秒一次。",
            ArtifactTrigger.Pursuit => "普攻命中有反击标记的敌人，消耗标记，追加自身攻击50%的伤害。",
            ArtifactTrigger.Remedy => "每3次普攻命中，为气血比例最低的队友恢复其气血上限的6%，可治疗自己。",
            ArtifactTrigger.Recovery => "受到装备回春治疗时，额外恢复自身气血上限的6%。",
            _ => ""
        };
        public int Price { get; internal set; }
        public int UpgradeBase { get; internal set; }
        public int UpgradeStep { get; internal set; }
        public int HealthAt(int level)=>checked(Health+(level-1)*HealthStep);
        public int AttackAt(int level)=>checked(Attack+(level-1)*AttackStep);
        public int UpgradePrice(int level)=>level>=MaxLevel?0:checked(UpgradeBase+(level-1)*UpgradeStep);
        public string Effect(int level)=>$"气血 +{HealthAt(level)} · 攻击 +{AttackAt(level)}";
    }

    public sealed class ArtifactInstance
    {
        public int Id { get; internal set; }
        public int DefinitionId { get; internal set; }
        public int SwordClears { get; internal set; }
        public int Level { get; internal set; }=1;
        // Role indices survive historical actor-ID remapping. -1 means shared bag.
        public int Owner { get; internal set; }=-1;
        public int Slot { get; internal set; }=-1;
        public ArtifactDefinition Definition=>ExpeditionEquipment.Definition(DefinitionId);
    }

    public sealed partial class ExpeditionEquipment
    {
        public static int SharedLoss(int incoming, int guardHealth) => Math.Min(guardHealth,(int)((long)incoming*15/100));
        public static int RingThreshold(int maxHealth) => Math.Max(1,(int)((15L*maxHealth+99)/100));
        public const int GrowingSwordCase = 17, QuakeSpike = 18, RainTassel = 19, RelayClasp = 20, ReprisalSeal = 21, HeartLamp = 22, TinderPouch = 23, MeridianSpike = 24, ThunderBell = 25, FalterMirror = 26, LastStandCharm = 27, AfflictionSeal = 28, EchoJade = 29, ComradeKnot = 30, FieldKit = 31, HiddenEdgePearl = 32, JadeRendBlade = 33, BreachMirror = 34, OpeningBrush = 35, StormCloud = 36, SunMirror = 37, EmberStamp = 38, TideChime = 39, SpiritSpindle = 40;
        private const int SwordProgressMarker = 20261121;
        private static int ItemStride(IReadOnlyList<int> data) => data[0] == SwordProgressMarker ? 6 : 5;

        internal ArtifactInstance[] TrainingCases(IEnumerable<ActorState> combatants)
        {
            if (!world.SwordTraining || !LinksEnabled) return Array.Empty<ArtifactInstance>();
            var owners = combatants.Where(a => party.Owns(a.Id)).Select(a => Index(a.Id)).ToArray();
            return instances.Where(item => item.DefinitionId == GrowingSwordCase && item.SwordClears < 2 &&
                owners.Contains(item.Owner)).ToArray();
        }

        internal void TrainSwordCases(ArtifactInstance[] worn)
        {
            if (worn.Length == 0) return;
            foreach (var item in worn) item.SwordClears++;
            party.RecordFormation();
        }

        private const int LinksMarker=20261120;
        public bool LinksEnabled { get; private set; }
        public void EnableLinks()
        {
            if (!Modern || LinksEnabled) return;
            LinksEnabled = true;
            party.RecordFormation();
        }
        public ArtifactTrigger BattleTriggers(int actor)
        {
            if (!LinksEnabled || !party.Owns(actor)) return ArtifactTrigger.None;
            var effects = Innate(actor)?.Trigger ?? ArtifactTrigger.None;
            int owner = Index(actor);
            foreach (var item in instances.Where(item => item.Owner == owner))
                if ((item.DefinitionId != GrowingSwordCase || item.SwordClears == 2) &&
                    (!item.Definition.Personal || item.DefinitionId == InnateDefinition(owner)))
                    effects |= item.Definition.Trigger;
            if ((effects & ArtifactTrigger.RelayClasp) != 0) effects |= ArtifactTrigger.Pursuit;
            return effects;
        }
        private const int ExpandedMarker=20261119;
        private const int ModernMarker=20261118;
        public const int CommonSlots=6, MaxInstances=1000, MaxSnapshot=10000;
        private static IReadOnlyList<ArtifactDefinition> definitions;
        public static Func<string> DefinitionTableLoader { get; set; }
        public static IReadOnlyList<ArtifactDefinition> Definitions
        {
            get
            {
                if(definitions!=null)return definitions;
                if(DefinitionTableLoader!=null)LoadDefinitions(DefinitionTableLoader());
                else
                using(var stream=typeof(ExpeditionEquipment).Assembly.GetManifestResourceStream("ExpeditionItems.csv"))
                using(var reader=new StreamReader(stream??throw new InvalidOperationException("Missing ExpeditionItems.csv.")))
                    LoadDefinitions(reader.ReadToEnd());
                return definitions;
            }
        }
        private static void LoadDefinitions(string csv)
        {
            var rows=new List<ArtifactDefinition>();
            foreach(string raw in csv.Split('\n').Skip(1))
            {
                string line=raw.Trim();if(line.Length==0 || line.StartsWith("#"))continue;
                var c=line.Split(',');if(c.Length!=13)throw new InvalidDataException("Invalid artifact table row.");
                int N(int i)=>int.Parse(c[i],CultureInfo.InvariantCulture);
                var d=new ArtifactDefinition{Id=N(0),Name=c[1],Innate=N(2)==1,Icon=N(3),Health=N(4),Attack=N(5),
                    HealthStep=N(6),AttackStep=N(7),MaxLevel=N(8),Price=N(9),UpgradeBase=N(10),UpgradeStep=N(11),Trigger=(ArtifactTrigger)Enum.Parse(typeof(ArtifactTrigger),c[12])};
                if(d.Id!=rows.Count+1 || d.MaxLevel<1 || d.MaxLevel>20 || d.Icon<0 || d.Icon>(d.Id==OpeningBrush?10:9) ||
                    new[]{d.Health,d.Attack,d.HealthStep,d.AttackStep,d.Price,d.UpgradeBase,d.UpgradeStep}.Any(v=>v<0 || v>100000) ||
                    d.Innate!=(d.Id<=4 || d.Id is >=14 and <=16 || d.Id==OpeningBrush) || (!d.Innate && d.Price<1) || d.UpgradeBase<1)
                    throw new InvalidDataException("Invalid artifact definition "+d.Id);
                rows.Add(d);
            }
            if(rows.Count!=SpiritSpindle)throw new InvalidDataException("Incomplete artifact table.");
            definitions=rows.AsReadOnly();
        }
        public static ArtifactDefinition Definition(int id)=>id>=1 && id<=Definitions.Count?Definitions[id-1]:null;
        private readonly List<ArtifactInstance> instances=new List<ArtifactInstance>();
        private readonly HashSet<int> eventVisits=new HashSet<int>();
        private readonly int[] innateLevels=new int[5 + ExpeditionParty.MaxPastLeaders];
        internal void GrantNamedInnate(int role) => innateLevels[role+1] = 1;
        private int InnateDefinition(int owner)
        {
            if (owner == 0 || owner > ExpeditionParty.StageRoles && owner <= party.RoleCount)
            {
                int birth = owner == 0 ? Leader.BirthNodeId : party.PastBirth(owner-1);
                if (birth == HexWorldMap.Id(-10,2)) return OpeningBrush;
                if (!world.SignatureArtifacts) return 1;
                if (birth == HexWorldMap.Id(0,-10)) return 14;
                if (birth == HexWorldMap.Id(10,-8)) return 15;
                if (birth == HexWorldMap.Id(8,2)) return 16;
            }
            return owner switch { 1 => 3, 3 => 2, 4 => 4, _ => 1 };
        }

        internal bool CanImportLoadout(ExpeditionEquipment source) =>
            instances.Count + (source.Modern ? source.instances.Count(item => item.Owner == 0) : source.equipped.Take(3).Count(id => id != 0)) <= MaxInstances;

        internal void ClearReturnedInnate(int role) => innateLevels[role+1] = 0;

        public IEnumerable<(int Definition,int Level)> DisbandedInnates()
        {
            foreach (int role in party.LivingRoles)
                if (innateLevels[role+1] > 0) yield return (InnateDefinition(role+1),innateLevels[role+1]);
        }

        internal bool CanMerge(ExpeditionEquipment source)
        {
            int count = source.instances.Count + source.DisbandedInnates().Count();
            return Modern && source.Modern && instances.Count + count <= MaxInstances &&
                nextInstance + (long)count <= 1000000 && SpiritStones <= long.MaxValue - source.SpiritStones;
        }

        // The captain's worn items have already been copied into the new member's slots.
        internal void MergeBags(ExpeditionEquipment source)
        {
            foreach (var item in source.instances.Where(i => i.Owner != 0))
                instances.Add(new ArtifactInstance { Id=nextInstance++, DefinitionId=item.DefinitionId, Level=item.Level, SwordClears=item.SwordClears });
            foreach (var innate in source.DisbandedInnates())
                instances.Add(new ArtifactInstance { Id=nextInstance++, DefinitionId=innate.Definition, Level=innate.Level });
            source.instances.Clear();
            Array.Clear(source.innateLevels,0,source.innateLevels.Length);
        }

        internal void ImportLeaderLoadout(ExpeditionEquipment source, int role)
        {
            int owner = role + 1;
            if (source == null) return;
            innateLevels[owner] = source.innateLevels[0];
            if (!source.Modern)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    int legacy = source.equipped[slot];
                    if (legacy != 0) instances.Add(new ArtifactInstance { Id = nextInstance++, Owner = owner, Slot = slot,
                        DefinitionId = legacy < 5 ? 8 : legacy });
                }
            }
            // Legacy permanent upgrades are personal and follow the wearer.
            health[owner] = source.health[0];
            attack[owner] = source.attack[0];
            foreach (var item in source.instances.Where(item => item.Owner == 0))
                instances.Add(new ArtifactInstance { Id = nextInstance++, DefinitionId = item.DefinitionId,
                    Level = item.Level, SwordClears = item.SwordClears, Slot = item.Slot, Owner = owner });
        }
        private int nextInstance=1;
        public bool Modern { get; private set; }
        public IReadOnlyList<ArtifactInstance> Instances=>instances.AsReadOnly();
        public long SpiritStones=>Leader.LegacySpiritStones;
        public ArtifactDefinition Innate(int actor)=>InnateLevel(actor)>0?Definition(InnateDefinition(Index(actor))):null;
        public int InnateLevel(int actor)=>party.Owns(actor)?innateLevels[Index(actor)]:0;
        public ArtifactInstance Instance(int id)=>instances.FirstOrDefault(i=>i.Id==id);
        public ArtifactInstance Worn(int actor,int slot)=>party.Owns(actor)&&slot>=0&&slot<CommonSlots?
            instances.FirstOrDefault(i=>i.Owner==Index(actor)&&i.Slot==slot):null;
        public int FirstFreeSlot(int actor)
        {for(int s=0;s<CommonSlots;s++)if(Worn(actor,s)==null)return s;return -1;}
        public void EnableArtifacts()
        {
            if(Modern)return;
            bool migrate=Enabled;Modern=true;Enabled=true;
            for(int i=0;i<5;i++)innateLevels[i]=1;
            if(migrate)
            {
                // Preserve every previously owned copy. Old disposable bonuses remain personal.
                for(int i=0;i<10;i++)for(int n=0;n<bag[i];n++)GrantInstance(i<4?8:i+1);
                for(int i=0;i<15;i++)if(equipped[i]!=0)
                {
                    GrantInstance(equipped[i]<5?8:equipped[i]);
                    var copy=instances.Last();copy.Owner=i/3;copy.Slot=i%3;
                }
            }
            else{GrantInstance(5);GrantInstance(7);GrantInstance(8);}
            Array.Clear(bag,0,bag.Length);Array.Clear(equipped,0,equipped.Length);
            UpdateLeader();party.RecordFormation();
        }
        private bool GrantInstance(int definition)
        {
            if(Definition(definition)?.Innate!=false || instances.Count>=MaxInstances)return false;
            instances.Add(new ArtifactInstance{Id=nextInstance++,DefinitionId=definition});return true;
        }
        private int ModernBonus(int role,bool hp)
        {
            int total=hp?health[role]:attack[role];
            var innate=Definition(InnateDefinition(role));
            if (innateLevels[role]>0) total+=hp?innate.HealthAt(innateLevels[role]):innate.AttackAt(innateLevels[role]);
            foreach(var item in instances.Where(i=>i.Owner==role))
                total+=hp?item.Definition.HealthAt(item.Level):item.Definition.AttackAt(item.Level);
            return total;
        }
        public bool EquipInstance(int actor,int instance,int slot,bool transfer=false)
        {
            var item=Instance(instance);
            if(!Modern || !party.Owns(actor) || slot<0 || slot>=CommonSlots || item==null)return false;
            if(transfer ? item.Owner<0 || item.Owner==Index(actor) : item.Owner>=0)return false;
            var previous=Worn(actor,slot);if(previous!=null){previous.Owner=-1;previous.Slot=-1;}
            item.Owner=Index(actor);item.Slot=slot;UpdateLeader();party.RecordFormation();return true;
        }
        private bool RemoveInstance(int actor,int slot)
        {
            var item=Worn(actor,slot);if(item==null)return false;
            item.Owner=-1;item.Slot=-1;UpdateLeader();party.RecordFormation();return true;
        }
        public bool IsForSale(ArtifactDefinition item) => item != null && !item.Innate && (item.Id != GrowingSwordCase || world.SwordTraining) &&
            (item.Id != QuakeSpike || world.QuakeSpikeEnabled) && (item.Id != RainTassel || world.RainTasselEnabled) &&
            (item.Id != RelayClasp || world.RelayClaspEnabled) && (item.Id != ReprisalSeal || world.ReprisalSealEnabled) &&
            (item.Id != HeartLamp || world.HeartLampEnabled) && (item.Id != TinderPouch || world.TinderPouchEnabled) &&
            (item.Id != MeridianSpike || world.MeridianSpikeEnabled) && (item.Id != ThunderBell || world.ThunderBellEnabled) &&
            (item.Id != FalterMirror || world.FalterMirrorEnabled) && (item.Id != LastStandCharm || world.LastStandCharmEnabled) &&
            (item.Id != AfflictionSeal || world.AfflictionSealEnabled) && (item.Id < EchoJade || item.Id > FieldKit || world.SupportArtifactsEnabled) &&
            (item.Id < HiddenEdgePearl || item.Id > BreachMirror || world.CriticalArtifactsEnabled);
        public bool Buy(int definition)
        {
            var d=Definition(definition);
            if(!Modern || !IsForSale(d) || instances.Count>=MaxInstances || !Leader.TrySpendLegacySpiritStones(d.Price))return false;
            GrantInstance(definition);party.RecordFormation();return true;
        }
        // Positive keys identify ordinary instances. Negative keys identify a role's fixed artifact.
        public int InnateKey(int actor)=>InnateLevel(actor)>0?-Index(actor)-1:0;
        public ArtifactDefinition UpgradeDefinition(int key)=>key<0&&key>=-(party.RoleCount+1)&& innateLevels[-key-1]>0?Definition(InnateDefinition(-key-1)):Instance(key)?.Definition;
        public int UpgradeLevel(int key)=>key<0&&key>=-(party.RoleCount+1)?innateLevels[-key-1]:Instance(key)?.Level??0;
        public bool CanUpgrade(int key)
        {
            var d=UpgradeDefinition(key);if(!Modern || d==null || UpgradeLevel(key)>=d.MaxLevel)return false;
            int role=key<0?-key-1:Instance(key).Owner;
            return role<0 || (!party.IsFallen(role-1) && (role==0 || party.HasRecruited(role-1)));
        }
        public bool Upgrade(int key,bool free=false)
        {
            if(!CanUpgrade(key))return false;
            var d=UpgradeDefinition(key);int level=UpgradeLevel(key);
            if(!free && !Leader.TrySpendLegacySpiritStones(d.UpgradePrice(level)))return false;
            if(key<0)innateLevels[-key-1]++;else Instance(key).Level++;
            UpdateLeader();party.RecordFormation();return true;
        }
        public bool HasVisitedEvent(int node)=>eventVisits.Contains(node);
        public bool VisitEvent(int node)
        {
            if (!Modern || node < 0) return false;
            world.Nodes.TryGetValue(node,out var location);
            if (world.SharedClears && (location?.FirstClear?.ActorId != party.LeaderId || location.EventRewardTaken)) return false;
            if (!eventVisits.Add(node)) return false;
            if (location != null) location.EventRewardTaken = true;
            Leader.AwardLegacySpiritStones(12);party.RecordFormation();return true;
        }
        // Each reward selection records a formation immediately after appending its new instance.
        internal static int ReadArchivedReward(IReadOnlyList<int> data, out (int Id,int Definition) reward)
        {
            reward = default;
            if (data.Count == 0) return 0;
            if (data[0] != ModernMarker && data[0] != ExpandedMarker && data[0] != LinksMarker && data[0] != SwordProgressMarker) return data[0];
            int last = data.Count - ItemStride(data);
            if (last >= 0 && data[last] == data[2]-1) reward = (data[last],data[last+1]);
            return data[1];
        }

        internal void ReconcileArchivedRewards(IReadOnlyDictionary<int,(int Id,int Definition)> recorded)
        {
            foreach (var pair in recorded)
            {
                var item = Instance(pair.Value.Id);
                if (item == null || !world.Nodes.TryGetValue(pair.Key,out var node)) continue;
                if (node.FirstClear?.ActorId != party.LeaderId || node.FirstArtifact == 0) instances.Remove(item);
                else if (item.DefinitionId != node.FirstArtifact)
                {
                    item.DefinitionId = node.FirstArtifact;
                    item.SwordClears = 0;
                    item.Level = 1;
                }
            }
            int earned = 0;
            foreach (var node in world.Nodes.Values)
            {
                if (node.FirstClear?.ActorId != party.LeaderId || node.FirstArtifact == 0) continue;
                earned++;
                if (recorded.TryGetValue(node.Id,out var reward) && Instance(reward.Id) != null) continue;
                // This runs on a freshly loaded snapshot. Avoid IDs used by later archived awards.
                if (recorded.Count > 0) nextInstance = Math.Max(nextInstance, recorded.Values.Max(r => r.Id) + 1);
                GrantInstance(node.FirstArtifact);
            }
            RewardsTaken = earned;
            UpdateLeader();
        }

        private static bool IsArtifactPlan(IReadOnlyList<int> data) => data != null && data.Count > 0 &&
            (data[0] == ModernMarker || data[0] == ExpandedMarker || data[0] == LinksMarker || data[0] == SwordProgressMarker);

        private static Dictionary<int,ArtifactInstance> PlannedItems(IReadOnlyList<int> data)
        {
            var result = new Dictionary<int,ArtifactInstance>();
            if (!IsArtifactPlan(data)) return result;
            int start = data[0] == ModernMarker ? 3 : 4;
            int owners = start == 3 ? 5 : data[3];
            int events = start + 3*owners;
            for (int i = events + data[events] + 2; i < data.Count; i += ItemStride(data))
                result.Add(data[i],new ArtifactInstance { Id=data[i], DefinitionId=data[i+1],
                    SwordClears = data[0] == SwordProgressMarker ? data[i+5] : 0, Level=data[i+2], Owner=data[i+3]-1, Slot=data[i+4]-1 });
            return result;
        }

        public static IEnumerable<(int Id, bool LegacyConsumable)> ArchivedPossessions(RecordedExpedition formation)
        {
            var snapshot = formation.Equipment;
            if (snapshot.Count == 0) yield break;
            if (IsArtifactPlan(snapshot))
            {
                foreach (var item in PlannedItems(snapshot).Values)
                    yield return (item.DefinitionId, false);
            }
            else
            {
                foreach (var item in Catalog)
                    if (snapshot[item.Id] > 0 || snapshot.Skip(11).Take(15).Contains(item.Id))
                        yield return (item.Id, item.Consumable);
            }
        }

        private readonly Dictionary<int,int> historicalInstances = new Dictionary<int,int>();

        internal void ApplyHistoricalPlan(IReadOnlyList<int> before, IReadOnlyList<int> after, bool merchant, IReadOnlyDictionary<int,int> rewardOrigins, Func<int,int> mapRole)
        {
            if (!IsArtifactPlan(after) || (before != null && before.SequenceEqual(after))) return;
            var old = PlannedItems(before);
            bool hadPlan = IsArtifactPlan(before);
            var mapped = new HashSet<int>(historicalInstances.Values);
            foreach (var item in PlannedItems(after).Values.OrderBy(i => i.Id))
            {
                old.TryGetValue(item.Id,out var prior);
                ArtifactInstance live;
                if (historicalInstances.TryGetValue(item.Id,out int id))
                    live = Instance(id);
                else if (rewardOrigins.TryGetValue(item.Id,out int origin))
                {
                    live = world.Nodes.TryGetValue(origin,out var node) && node.FirstClear?.ActorId == party.LeaderId
                        ? Instance(node.FirstArtifactInstance) : null;
                }
                else if (prior == null && merchant && hadPlan && before[1] == after[1] && item.Owner < 0)
                    live = Buy(item.DefinitionId) ? instances.Last() : null;
                else
                    live = instances.FirstOrDefault(i => i.DefinitionId == item.DefinitionId && !mapped.Contains(i.Id));
                historicalInstances[item.Id] = live?.Id ?? 0;
                if (live == null) continue;
                mapped.Add(live.Id);
                if (merchant && prior != null && item.Level > prior.Level) Upgrade(live.Id);
                if (item.Owner == prior?.Owner && item.Slot == prior?.Slot) continue;
                if (item.Owner < 0)
                {
                    if (prior?.Owner >= 0 && live.Owner >= 0) RemoveInstance(party.FieldId(live.Owner-1),live.Slot);
                    continue;
                }
                int role = mapRole(item.Owner-1);
                if (role < -1 || role >= party.RoleCount || party.IsFallen(role)) continue;
                int actor = party.FieldId(role);
                if (!party.Owns(actor)) continue;
                if (live.Owner == role+1)
                {
                    if (live.Slot == item.Slot) continue;
                    RemoveInstance(actor,live.Slot);
                }
                EquipInstance(actor,live.Id,item.Slot,live.Owner >= 0);
            }
            if (!merchant || !hadPlan) return;
            int first = after[0] == ModernMarker ? 3 : 4;
            int prev = before[0] == ModernMarker ? 3 : 4;
            int owners = Math.Min(first == 3 ? 5 : after[3],prev == 3 ? 5 : before[3]);
            for (int owner = 0; owner < owners; owner++)
                if (after[first+owner] > before[prev+owner] && mapRole(owner-1) is int role && role >= -1) Upgrade(-role-2);
        }

        private int[] ModernSnapshot()
        {
            int owners = party.RoleCount + 1;
            bool withProgress = LinksEnabled && instances.Any(i => i.DefinitionId == GrowingSwordCase);
            var result=new List<int>{withProgress ? SwordProgressMarker : LinksEnabled ? LinksMarker : owners == 5 ? ModernMarker : ExpandedMarker,RewardsTaken,nextInstance};
            if (owners > 5 || LinksEnabled) result.Add(owners);
            result.AddRange(innateLevels.Take(owners));result.AddRange(health.Take(owners));result.AddRange(attack.Take(owners));
            result.Add(eventVisits.Count);result.AddRange(eventVisits.OrderBy(n=>n));result.Add(instances.Count);
            foreach(var i in instances)
            {
                result.AddRange(new[]{i.Id,i.DefinitionId,i.Level,i.Owner+1,i.Slot+1});
                if (withProgress) result.Add(i.SwordClears);
            }
            return result.ToArray();
        }
        private static void ValidateModernSnapshot(IReadOnlyList<int> d)
        {
            if(d.Count<20 || d.Count>MaxSnapshot || d.Skip(1).Any(v=>v<0 || v>1000000) || d[2]<1)
                throw new ArgumentException("Invalid artifact snapshot.");
            int start = d[0] == ModernMarker ? 3 : 4, owners = start == 4 ? d[3] : 5;
            if (owners < 5 || owners > 5 + ExpeditionParty.MaxPastLeaders || d.Count < start + owners*3 + 2)
                throw new ArgumentException("Invalid artifact owners.");
            for(int i=0;i<owners;i++)if(d[start+i]<(i==0?1:0) || d[start+i]>5)
                throw new ArgumentException("Invalid innate level.");
            int firstEvent=start+owners*3+1, events=d[firstEvent-1], at=firstEvent+events;
            if(events>1000 || at>=d.Count || d.Skip(firstEvent).Take(events).Distinct().Count()!=events || d[at]>MaxInstances+owners || d.Count!=at+1+d[at]*ItemStride(d))
                throw new ArgumentException("Invalid artifact payload size.");
            var ids=new HashSet<int>();var slots=new HashSet<int>();
            for(int i=at+1;i<d.Count;i+=ItemStride(d))
            {
                var def=Definition(d[i+1]);int owner=d[i+3],slot=d[i+4];
                if (d[0] == SwordProgressMarker && d[i+5] > (d[i+1] == GrowingSwordCase ? 2 : 0))
                    throw new ArgumentException("Invalid sword training progress.");
                if(d[i]<1 || d[i]>=d[2] || !ids.Add(d[i]) || def==null || d[i+2]<1 || d[i+2]>def.MaxLevel ||
                    owner>owners || slot>CommonSlots || (owner==0)!=(slot==0) || (owner>0&&!slots.Add(owner*CommonSlots+slot)))
                    throw new ArgumentException("Invalid artifact instance.");
            }
        }
        private void RestoreModern(IReadOnlyList<int> d)
        {
            LinksEnabled=d[0]==LinksMarker || d[0]==SwordProgressMarker;Modern=true;Enabled=true;RewardsTaken=d[1];nextInstance=d[2];pending=Array.Empty<int>();
            int start = d[0] == ModernMarker ? 3 : 4, owners = start == 4 ? d[3] : 5;
            for(int i=0;i<owners;i++){innateLevels[i]=d[start+i];health[i]=d[start+owners+i];attack[i]=d[start+owners*2+i];}
            int firstEvent=start+owners*3+1, events=d[firstEvent-1],at=firstEvent+events;
            for(int i=0;i<events;i++)eventVisits.Add(d[firstEvent+i]);
            for(int i=at+1;i<d.Count;i+=ItemStride(d))instances.Add(new ArtifactInstance{Id=d[i],DefinitionId=d[i+1],SwordClears = d[0] == SwordProgressMarker ? d[i+5] : 0, Level=d[i+2],Owner=d[i+3]-1,Slot=d[i+4]-1});
        }
    }
}
