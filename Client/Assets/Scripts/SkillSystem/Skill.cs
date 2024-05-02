using liuchengguanli;

namespace SkillSystem
{
    public class Skill
    {
        public int SkillID;
        public SkillType CurSkillType;
        public TriggerList OwnTriggerList;
        public EntityQizi Caster;
        public int CDMs;
        public int ShakeBeforeMs;//技能前摇

        public void Cast()
        {
            
        }
    }
}