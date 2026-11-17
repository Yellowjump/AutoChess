using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WorldTimeline
{
    public sealed class ArtifactDefinition
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public bool Innate { get; internal set; }
        public int Icon { get; internal set; }
        public int Health { get; internal set; }
        public int Attack { get; internal set; }
        public int HealthStep { get; internal set; }
        public int AttackStep { get; internal set; }
        public int MaxLevel { get; internal set; }
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
        public int Level { get; internal set; }=1;
        // Role indices survive historical actor-ID remapping. -1 means shared bag.
        public int Owner { get; internal set; }=-1;
        public int Slot { get; internal set; }=-1;
        public ArtifactDefinition Definition=>ExpeditionEquipment.Definition(DefinitionId);
    }

    public sealed partial class ExpeditionEquipment
    {
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
                var c=line.Split(',');if(c.Length!=12)throw new InvalidDataException("Invalid artifact table row.");
                int N(int i)=>int.Parse(c[i],CultureInfo.InvariantCulture);
                var d=new ArtifactDefinition{Id=N(0),Name=c[1],Innate=N(2)==1,Icon=N(3),Health=N(4),Attack=N(5),
                    HealthStep=N(6),AttackStep=N(7),MaxLevel=N(8),Price=N(9),UpgradeBase=N(10),UpgradeStep=N(11)};
                if(d.Id!=rows.Count+1 || d.MaxLevel<1 || d.MaxLevel>20 || d.Icon<0 || d.Icon>9 ||
                    new[]{d.Health,d.Attack,d.HealthStep,d.AttackStep,d.Price,d.UpgradeBase,d.UpgradeStep}.Any(v=>v<0 || v>100000) ||
                    d.Innate!=(d.Id<=4) || (!d.Innate && d.Price<1) || d.UpgradeBase<1)
                    throw new InvalidDataException("Invalid artifact definition "+d.Id);
                rows.Add(d);
            }
            if(rows.Count!=10)throw new InvalidDataException("Artifact table must contain ten definitions.");
            definitions=rows.AsReadOnly();
        }
        public static ArtifactDefinition Definition(int id)=>id>=1 && id<=Definitions.Count?Definitions[id-1]:null;
        private readonly List<ArtifactInstance> instances=new List<ArtifactInstance>();
        private readonly HashSet<int> eventVisits=new HashSet<int>();
        private readonly int[] innateLevels=new int[5];
        private int nextInstance=1;
        public bool Modern { get; private set; }
        public IReadOnlyList<ArtifactInstance> Instances=>instances.AsReadOnly();
        public long SpiritStones=>Leader.LegacySpiritStones;
        public ArtifactDefinition Innate(int actor)=>party.Owns(actor)?Definition(new[]{1,3,1,2,4}[Index(actor)]):null;
        public int InnateLevel(int actor)=>party.Owns(actor)?innateLevels[Index(actor)]:0;
        public ArtifactInstance Instance(int id)=>instances.FirstOrDefault(i=>i.Id==id);
        public ArtifactInstance Worn(int actor,int slot)=>party.Owns(actor)&&slot>=0&&slot<CommonSlots?
            instances.FirstOrDefault(i=>i.Owner==Index(actor)&&i.Slot==slot):null;
        private int FirstFreeSlot(int actor)
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
            var innate=Definition(new[]{1,3,1,2,4}[role]);
            total+=hp?innate.HealthAt(innateLevels[role]):innate.AttackAt(innateLevels[role]);
            foreach(var item in instances.Where(i=>i.Owner==role))
                total+=hp?item.Definition.HealthAt(item.Level):item.Definition.AttackAt(item.Level);
            return total;
        }
        public bool EquipInstance(int actor,int instance,int slot)
        {
            var item=Instance(instance);
            if(!Modern || !party.Owns(actor) || slot<0 || slot>=CommonSlots || item==null || item.Owner>=0)return false;
            var previous=Worn(actor,slot);if(previous!=null){previous.Owner=-1;previous.Slot=-1;}
            item.Owner=Index(actor);item.Slot=slot;UpdateLeader();party.RecordFormation();return true;
        }
        private bool RemoveInstance(int actor,int slot)
        {
            var item=Worn(actor,slot);if(item==null)return false;
            item.Owner=-1;item.Slot=-1;UpdateLeader();party.RecordFormation();return true;
        }
        public bool Buy(int definition)
        {
            var d=Definition(definition);
            if(!Modern || d==null || d.Innate || instances.Count>=MaxInstances || !Leader.TrySpendLegacySpiritStones(d.Price))return false;
            GrantInstance(definition);party.RecordFormation();return true;
        }
        // Positive keys identify ordinary instances. Negative keys identify a role's fixed artifact.
        public int InnateKey(int actor)=>party.Owns(actor)?-Index(actor)-1:0;
        public ArtifactDefinition UpgradeDefinition(int key)=>key<0&&key>=-5?Definition(new[]{1,3,1,2,4}[-key-1]):Instance(key)?.Definition;
        public int UpgradeLevel(int key)=>key<0&&key>=-5?innateLevels[-key-1]:Instance(key)?.Level??0;
        public bool CanUpgrade(int key)
        {
            var d=UpgradeDefinition(key);if(!Modern || d==null || UpgradeLevel(key)>=d.MaxLevel)return false;
            int role=key<0?-key-1:Instance(key).Owner;
            return role<=0 || party.HasRecruited(role-1);
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
            if(!Modern || node<0 || !eventVisits.Add(node))return false;
            Leader.AwardLegacySpiritStones(12);party.RecordFormation();return true;
        }
        private int[] ModernSnapshot()
        {
            var result=new List<int>{ModernMarker,RewardsTaken,nextInstance};
            result.AddRange(innateLevels);result.AddRange(health);result.AddRange(attack);
            result.Add(eventVisits.Count);result.AddRange(eventVisits.OrderBy(n=>n));result.Add(instances.Count);
            foreach(var i in instances)result.AddRange(new[]{i.Id,i.DefinitionId,i.Level,i.Owner+1,i.Slot+1});
            return result.ToArray();
        }
        private static void ValidateModernSnapshot(IReadOnlyList<int> d)
        {
            if(d.Count<20 || d.Count>MaxSnapshot || d.Skip(1).Any(v=>v<0 || v>1000000) || d[2]<1)
                throw new ArgumentException("Invalid artifact snapshot.");
            for(int i=0;i<5;i++)if(d[3+i]<1 || d[3+i]>Definition(new[]{1,3,1,2,4}[i]).MaxLevel)
                throw new ArgumentException("Invalid innate level.");
            int events=d[18], at=19+events;
            if(events>1000 || at>=d.Count || d.Skip(19).Take(events).Distinct().Count()!=events || d[at]>MaxInstances || d.Count!=at+1+d[at]*5)
                throw new ArgumentException("Invalid artifact payload size.");
            var ids=new HashSet<int>();var slots=new HashSet<int>();
            for(int i=at+1;i<d.Count;i+=5)
            {
                var def=Definition(d[i+1]);int owner=d[i+3],slot=d[i+4];
                if(d[i]<1 || d[i]>=d[2] || !ids.Add(d[i]) || def==null || def.Innate || d[i+2]<1 || d[i+2]>def.MaxLevel ||
                    owner>5 || slot>CommonSlots || (owner==0)!=(slot==0) || (owner>0&&!slots.Add(owner*CommonSlots+slot)))
                    throw new ArgumentException("Invalid artifact instance.");
            }
        }
        private void RestoreModern(IReadOnlyList<int> d)
        {
            Modern=true;Enabled=true;RewardsTaken=d[1];nextInstance=d[2];pending=Array.Empty<int>();
            for(int i=0;i<5;i++){innateLevels[i]=d[3+i];health[i]=d[8+i];attack[i]=d[13+i];}
            int events=d[18],at=19+events;for(int i=0;i<events;i++)eventVisits.Add(d[19+i]);
            for(int i=at+1;i<d.Count;i+=5)instances.Add(new ArtifactInstance{Id=d[i],DefinitionId=d[i+1],Level=d[i+2],Owner=d[i+3]-1,Slot=d[i+4]-1});
        }
    }
}
