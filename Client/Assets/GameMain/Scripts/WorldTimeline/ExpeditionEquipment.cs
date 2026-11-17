using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldTimeline
{
    public sealed class ExpeditionItem
    {
        public int Id { get; }
        public string Name { get; }
        public int Slot { get; } // Weapon, armor, accessory. -1 is consumable.
        public int Health { get; }
        public int Attack { get; }
        public int Icon { get; }
        private readonly int roles;
        public bool Consumable => Slot < 0;
        public bool Fits(int role) => (roles & (1 << (role + 1))) != 0;
        public string Effect => (Health > 0 ? $"气血上限 +{Health}" : "") +
            (Health > 0 && Attack > 0 ? " · " : "") + (Attack > 0 ? $"攻击 +{Attack}" : "");
        public string Description => Consumable ? "使用后永久提升所选角色的属性，消耗一份。" :
            "穿戴后生效，替换或卸下时返还共享行囊。";
        internal ExpeditionItem(int id,string name,int slot,int health,int attack,int icon,int roles=31)
        { Id=id;Name=name;Slot=slot;Health=health;Attack=attack;Icon=icon;this.roles=roles; }
    }

    public sealed partial class ExpeditionEquipment
    {
        public static readonly IReadOnlyList<ExpeditionItem> Catalog=Array.AsReadOnly(new[]
        {
            new ExpeditionItem(1,"青锋剑",0,0,8,0,5),
            new ExpeditionItem(2,"桑木弓",0,0,7,1,8),
            new ExpeditionItem(3,"铁木盾",0,100,2,2,2),
            new ExpeditionItem(4,"药师杖",0,40,4,3,16),
            new ExpeditionItem(5,"素布袍",1,60,0,4),
            new ExpeditionItem(6,"锁子甲",1,110,0,5),
            new ExpeditionItem(7,"护身玉",2,40,3,6),
            new ExpeditionItem(8,"砺锋符",2,0,5,7),
            new ExpeditionItem(9,"淬体丹",-1,20,0,8),
            new ExpeditionItem(10,"凝气散",-1,0,2,9)
        });
        public static ExpeditionItem Item(int id) => id>=1 && id<=Catalog.Count?Catalog[id-1]:null;
        public static string SlotName(int slot) => slot switch {0=>"兵器",1=>"护具",2=>"饰物",_=>"道具"};
        private readonly ExpeditionParty party;
        private readonly WorldState world;
        private readonly int[] bag=new int[10], equipped=new int[15], health=new int[5], attack=new int[5];
        private int appliedHealth, appliedAttack;
        private int[] pending=Array.Empty<int>();
        public bool Enabled { get; private set; }
        public int RewardsTaken { get; private set; }
        public IReadOnlyList<int> PendingRewards => Array.AsReadOnly(pending);
        public int Count(int item) => Modern?instances.Count(i=>i.DefinitionId==item && i.Owner<0):Item(item)!=null?bag[item-1]:0;
        public int Equipped(int actor,int slot) => Modern?Worn(actor,slot)?.DefinitionId??0:party.Owns(actor) && slot>=0 && slot<3?equipped[Index(actor)*3+slot]:0;
        private int Index(int actor) => actor==party.LeaderId?0:ExpeditionParty.Role(actor)+1;
        private ActorState Leader => world.Actors[party.LeaderId];
        internal ExpeditionEquipment(ExpeditionParty party,WorldState world) {this.party=party;this.world=world;}
        public void Enable()
        {
            if(Enabled)return;
            Enabled=true;
            for(int i=0;i<5;i++)bag[i]=1;
            bag[8]=2;bag[9]=1;
            party.RecordFormation();
        }
        public int HealthBonus(int actor) => Bonus(Index(actor),true);
        public int AttackBonus(int actor) => Bonus(Index(actor),false);
        private int Bonus(int index,bool hp)
        {
            if(Modern)return ModernBonus(index,hp);
            int total=hp?health[index]:attack[index];
            for(int s=0;s<3;s++)
            {
                var item=Item(equipped[index*3+s]);
                if(item!=null)total+=hp?item.Health:item.Attack;
            }
            return total;
        }
        public int UnmodifiedLeaderHealth => Leader.MaxHealth-appliedHealth;
        public int UnmodifiedLeaderAttack => Leader.Attack-appliedAttack;
        public bool Equip(int actor,int itemId)
        {
            if(Modern)return EquipInstance(actor,instances.FirstOrDefault(i=>i.Owner<0 && i.DefinitionId==itemId)?.Id??0,FirstFreeSlot(actor));
            var item=Item(itemId);
            if(!Enabled || !party.Owns(actor) || item==null || item.Consumable || Count(itemId)<1 ||
                !item.Fits(actor==party.LeaderId?-1:ExpeditionParty.Role(actor)))return false;
            int cell=Index(actor)*3+item.Slot, previous=equipped[cell];
            if(previous==itemId)return false;
            if(previous!=0)bag[previous-1]++;
            bag[itemId-1]--;equipped[cell]=itemId;
            UpdateLeader();party.RecordFormation();return true;
        }
        public bool Unequip(int actor,int slot)
        {
            if(Modern)return RemoveInstance(actor,slot);
            if(!Enabled || !party.Owns(actor) || slot<0 || slot>2)return false;
            int cell=Index(actor)*3+slot, item=equipped[cell];
            if(item==0)return false;
            bag[item-1]++;equipped[cell]=0;
            UpdateLeader();party.RecordFormation();return true;
        }
        public bool Use(int actor,int itemId)
        {
            if(Modern)return false;
            var item=Item(itemId);
            if(!Enabled || !party.Owns(actor) || item==null || !item.Consumable || Count(itemId)<1)return false;
            int index=Index(actor);
            // Validate totals before consuming an item.
            int hp=checked(health[index]+item.Health), atk=checked(attack[index]+item.Attack);
            bag[itemId-1]--;health[index]=hp;attack[index]=atk;
            UpdateLeader();party.RecordFormation();return true;
        }
        private void UpdateLeader()
        {
            int hp=Bonus(0,true), atk=Bonus(0,false);
            Leader.MaxHealth=checked(Leader.MaxHealth+hp-appliedHealth);
            Leader.Attack=checked(Leader.Attack+atk-appliedAttack);
            Leader.Health=Math.Min(Leader.Health,Leader.MaxHealth);
            appliedHealth=hp;appliedAttack=atk;
        }
        public IReadOnlyList<int> OfferReward(int node)
        {
            if(!Enabled || !Leader.IsAlive)return Array.Empty<int>();
            if(pending.Length>0)return PendingRewards;
            if(Modern)
            {
                int start=(int)(unchecked((uint)node*397u+(uint)RewardsTaken*17u)%6u);
                pending=new[]{5+start,5+(start+2)%6,5+(start+4)%6};return PendingRewards;
            }
            var weapons=Catalog.Where(i=>i.Slot==0 && Enumerable.Range(-1,5).Any(role=>
                (role==-1 || party.HasRecruited(role)) && i.Fits(role))).ToArray();
            uint seed=unchecked((uint)(node*397)^((uint)RewardsTaken*2654435761u));
            pending=new[]{weapons[seed%(uint)weapons.Length].Id,5+(int)((seed>>4)%4),9+(int)((seed>>8)%2)};
            return PendingRewards;
        }
        public bool ClaimReward(int option)
        {
            if(option<0 || option>=pending.Length)return false;
            if(Modern){if(!GrantInstance(pending[option]))return false;}
            else
            bag[pending[option]-1]++;RewardsTaken++;pending=Array.Empty<int>();
            party.RecordFormation();return true;
        }
        // Fixed, bounded payload. Role indices are independent of historical actor IDs.
        public int[] Snapshot() => Modern?ModernSnapshot():!Enabled?Array.Empty<int>():
            new[]{RewardsTaken}.Concat(bag).Concat(equipped).Concat(health).Concat(attack).ToArray();
        public static void ValidateSnapshot(IReadOnlyList<int> data)
        {
            if(data.Count==0)return;
            if(data[0]==ModernMarker){ValidateModernSnapshot(data);return;}
            if(data.Count!=36 || data.Any(v=>v<0 || v>1000000))
                throw new ArgumentException("Invalid expedition equipment snapshot.");
            for(int i=0;i<15;i++)
            {
                var item=Item(data[11+i]);
                if(data[11+i]!=0 && (item==null || item.Slot!=i%3 || !item.Fits(i/3-1)))
                    throw new ArgumentException("Invalid equipped item.");
            }
        }
        internal void Restore(IReadOnlyList<int> data)
        {
            ValidateSnapshot(data);
            Modern=false;instances.Clear();eventVisits.Clear();Array.Clear(innateLevels,0,5);
            if(data.Count>0 && data[0]==ModernMarker){RestoreModern(data);UpdateLeader();return;}
            Array.Clear(bag,0,bag.Length);Array.Clear(equipped,0,equipped.Length);
            Array.Clear(health,0,health.Length);Array.Clear(attack,0,attack.Length);
            Enabled=data.Count>0;RewardsTaken=Enabled?data[0]:0;pending=Array.Empty<int>();
            if(Enabled)
            {
                for(int i=0;i<10;i++)bag[i]=data[1+i];
                for(int i=0;i<15;i++)equipped[i]=data[11+i];
                for(int i=0;i<5;i++){health[i]=data[26+i];attack[i]=data[31+i];}
            }
            UpdateLeader();
        }
    }
}
