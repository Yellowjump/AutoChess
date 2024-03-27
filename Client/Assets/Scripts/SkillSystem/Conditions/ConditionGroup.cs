using System.Collections.Generic;

namespace SkillSystem
{
    public class ConditionGroup:ConditionBase
    {
        public override ConditionType CurConditionType => ConditionType.ConditionGroup;
        public LogicOperator CurLogicOperator = LogicOperator.And;
        public List<ConditionBase> ConditionList = new List<ConditionBase>();
        public override bool OnCheck(OneTrigger trigger)
        {
            if (ConditionList == null || ConditionList.Count == 0)
            {
                return true;
            }

            bool tempResult = CurLogicOperator == LogicOperator.And ? true : false;
            foreach (var oneCondition in ConditionList)
            {
                bool oneResult = oneCondition.OnCheck(trigger) == !oneCondition.ReverseResult;
                switch (CurLogicOperator)
                {
                    case LogicOperator.And:
                        tempResult = tempResult && oneResult;
                        break;
                    case LogicOperator.Or:
                        tempResult = tempResult || oneResult;
                        break;
                }

                if ((CurLogicOperator == LogicOperator.And && !tempResult) || (CurLogicOperator == LogicOperator.Or && tempResult))
                {
                    return tempResult; // 如果遇到false（与门）或true（或门），直接返回结果
                }
            }
            return tempResult;
        }
    }
}