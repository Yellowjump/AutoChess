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
        public int SkillRange;//攻击距离
        public TriggerList OwnTriggerList;
        public EntityQizi Caster;
        public int DefaultAnimationDurationMs;//默认技能动画时长，也是默认技能时长
        public int DefaultSkillCDMs;//默认技能CD
        public float LeftSkillCD;//剩余的冷却时间
        public bool InCD => LeftSkillCD > 0;// 技能还没冷却好
        public int ShakeBeforeMs;//技能前摇
        public SkillCastTargetType CurSkillCastTargetType;//释放目标类型
        public void Cast()
        {
            LeftSkillCD = DefaultSkillCDMs/1000.0f;//todo 读取角色CD缩减
            OwnTriggerList.OnActive();
        }

        public void OnDestory()
        {
            OwnTriggerList.OnDestory();
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

        public void LogicUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (LeftSkillCD > 0)
            {
                LeftSkillCD -= elapseSeconds;
            }
        }
    }
}