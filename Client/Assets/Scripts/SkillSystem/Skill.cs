using System.IO;
using Entity;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class Skill
    {
        public int SkillID;
        public int TempleteID;
        public SkillType CurSkillType;
        public TriggerList OwnTriggerList;
        public EntityQizi Caster;
        public int DefaultAnimationDurationMs;//默认技能动画时长，也是默认技能时长
        public int ShakeBeforeMs;//技能前摇
        public int AfterCastDurationMs;//释放之后经过的时间ms
        public SkillCastTargetType CurSkillCastTargetType;//释放目标类型
        public void Cast()
        {
            OwnTriggerList.OnActive();
        }
        public void Clone(Skill copy)
        {
            copy.TempleteID = TempleteID;
            copy.OwnTriggerList ??= SkillFactory.CreateNewEmptyTriggerList(copy);
            OwnTriggerList.Clone(copy.OwnTriggerList);
        }
        public void ReadFromFile(BinaryReader reader)
        {
            TempleteID = reader.ReadInt32();
            OwnTriggerList ??= SkillFactory.CreateNewEmptyTriggerList(this);
            OwnTriggerList.ReadFromFile(reader);
        }

        public void WriteToFile(BinaryWriter writer)
        {
            writer.Write(TempleteID);
            OwnTriggerList?.WriteToFile(writer);
        }
        public void SetSkillValue(DataRowBase dataTable)
        {
            OwnTriggerList?.SetSkillValue(dataTable);
        }

        public void OnSkillBeforeShakeEnd()
        {
            OwnTriggerList?.OnTrigger(TriggerType.SkillBeforeShakeEnd);
        }
    }
}