using GameFramework;
using GameFramework.Event;
using Maze;
using SkillSystem;

namespace SelfEventArg
{
    /// <summary>
    /// 事件点中 转为 战斗
    /// </summary>
    public class EventChangeToBattleEventArg:GameEventArgs
    {
        public static readonly int EventId = typeof(EventChangeToBattleEventArg).GetHashCode();
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 战斗关卡点ID
        /// </summary>
        public int BattleLevelID;
        public override void Clear()
        {
            BattleLevelID = 0;
        }
        public static EventChangeToBattleEventArg Create(int levelID)
        {
            EventChangeToBattleEventArg eventChangeToBattleEventArg = ReferencePool.Acquire<EventChangeToBattleEventArg>();
            eventChangeToBattleEventArg.BattleLevelID = levelID;
            return eventChangeToBattleEventArg;
        }
    }
    /// <summary>
    /// 事件点 完成 回到map
    /// </summary>
    public class EventCompleteToMapEventArg:GameEventArgs
    {
        public static readonly int EventId = typeof(EventCompleteToMapEventArg).GetHashCode();
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
        public static EventCompleteToMapEventArg Create()
        {
            EventCompleteToMapEventArg eventCompleteToMapEventArg = ReferencePool.Acquire<EventCompleteToMapEventArg>();
            return eventCompleteToMapEventArg;
        }
    }
}