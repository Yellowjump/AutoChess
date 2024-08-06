using GameFramework;
using GameFramework.Event;

namespace SelfEventArg
{
    /// <summary>
    /// 点击关卡
    /// </summary>
    public class CMDGetItemEventArgs:GameEventArgs
    {
        public static readonly int EventId = typeof(CMDGetItemEventArgs).GetHashCode();
        public override int Id
        {
            get
            {
                return EventId;
            }
        }
        /// <summary>
        /// 物品ID
        /// </summary>
        public int ItemID
        {
            get;
            private set;
        }
        /// <summary>
        /// 物品数量
        /// </summary>
        public int ItemNum
        {
            get;
            private set;
        }
        public override void Clear()
        {
            ItemID =0;
            ItemNum = 0;
        }
        public static CMDGetItemEventArgs Create(int id,int num)
        {
            CMDGetItemEventArgs cmdGetItemEventArgs = ReferencePool.Acquire<CMDGetItemEventArgs>();
            cmdGetItemEventArgs.ItemID = id;
            cmdGetItemEventArgs.ItemNum = num;
            return cmdGetItemEventArgs;
        }
    }
}