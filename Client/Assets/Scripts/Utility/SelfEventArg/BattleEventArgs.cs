using GameFramework;
using GameFramework.Event;
using Maze;
using SkillSystem;

namespace SelfEventArg
{
    /// <summary>
    /// 点击关卡
    /// </summary>
    public class EnterPointEventArgs:GameEventArgs
    {
        public static readonly int EventId = typeof(EnterPointEventArgs).GetHashCode();
        public override int Id
        {
            get
            {
                return EventId;
            }
        }
        /// <summary>
        /// 选中点。
        /// </summary>
        public MazePoint TargetPoint
        {
            get;
            private set;
        }
        public override void Clear()
        {
            TargetPoint = null;
        }
        public static EnterPointEventArgs Create(MazePoint point)
        {
            EnterPointEventArgs formationToBattleEventArgs = ReferencePool.Acquire<EnterPointEventArgs>();
            formationToBattleEventArgs.TargetPoint = point;
            return formationToBattleEventArgs;
        }
    }
    /// <summary>
    /// 编队完成事件编号。
    /// </summary>
    public class FormationToBattleEventArgs:GameEventArgs
    {
        public static readonly int EventId = typeof(FormationToBattleEventArgs).GetHashCode();
        /// <summary>
        /// 获取加载数据表时加载依赖资源事件编号。
        /// </summary>
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
        public static FormationToBattleEventArgs Create()
        {
            FormationToBattleEventArgs formationToBattleEventArgs = ReferencePool.Acquire<FormationToBattleEventArgs>();
            return formationToBattleEventArgs;
        }
    }
    /// <summary>
    /// 通过关卡
    /// </summary>
    public class PassPointEventArgs:GameEventArgs
    {
        public static readonly int EventId = typeof(PassPointEventArgs).GetHashCode();
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
        public static PassPointEventArgs Create()
        {
            PassPointEventArgs passPointEventArgs = ReferencePool.Acquire<PassPointEventArgs>();
            return passPointEventArgs;
        }
    }
    /// <summary>
    /// 战斗结束
    /// </summary>
    public class BattleStopEventArgs:GameEventArgs
    {
        public static readonly int EventId = typeof(BattleStopEventArgs).GetHashCode();
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        public bool Win = true;
        public override void Clear()
        {
            Win = true;
        }
        public static BattleStopEventArgs Create(bool win)
        {
            BattleStopEventArgs battleStopEventArgs = ReferencePool.Acquire<BattleStopEventArgs>();
            battleStopEventArgs.Win = win;
            return battleStopEventArgs;
        }
    }
    /// <summary>
    /// 回到主界面
    /// </summary>
    public class ReturnToTitleEventArgs:GameEventArgs
    {
        public static readonly int EventId = typeof(ReturnToTitleEventArgs).GetHashCode();
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
        public static ReturnToTitleEventArgs Create()
        {
            ReturnToTitleEventArgs passPointEventArgs = ReferencePool.Acquire<ReturnToTitleEventArgs>();
            return passPointEventArgs;
        }
    }
}