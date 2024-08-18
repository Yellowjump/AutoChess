using GameFramework;
using GameFramework.Event;

namespace SelfEventArg
{
    /// <summary>
    /// 背包切换到 装备
    /// </summary>
    public class BagPanelCheckToEquipEventArgs:GameEventArgs
    {
        public static readonly int EventId = typeof(BagPanelCheckToEquipEventArgs).GetHashCode();
        public override int Id
        {
            get
            {
                return EventId;
            }
        }
        
        public override void Clear()
        {
           
        }
        public static BagPanelCheckToEquipEventArgs Create()
        {
            BagPanelCheckToEquipEventArgs bagPanelCheckToEquipEventArgs = ReferencePool.Acquire<BagPanelCheckToEquipEventArgs>();
            return bagPanelCheckToEquipEventArgs;
        }
    }
    /// <summary>
    /// 背包切换到 合成
    /// </summary>
    public class BagPanelCheckToCraftEventArgs:GameEventArgs
    {
        public static readonly int EventId = typeof(BagPanelCheckToCraftEventArgs).GetHashCode();
        public override int Id
        {
            get
            {
                return EventId;
            }
        }
        
        public override void Clear()
        {
           
        }
        public static BagPanelCheckToCraftEventArgs Create()
        {
            BagPanelCheckToCraftEventArgs bagPanelCheckToEquipEventArgs = ReferencePool.Acquire<BagPanelCheckToCraftEventArgs>();
            return bagPanelCheckToEquipEventArgs;
        }
    }
}