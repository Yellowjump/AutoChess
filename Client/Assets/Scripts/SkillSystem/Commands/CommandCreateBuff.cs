using System.IO;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace SkillSystem
{
    public class CommandCreateBuff:CommandBase
    {
        public override CommandType CurCommandType => CommandType.CreateBuff;
        /// <summary>
        /// 是否创建buff表中的buff
        /// </summary>
        public bool UseTemplateBuff;
        public TableParamInt BuffID;
        /// <summary>
        /// <para>UseTemplateBuff 是 false时是临时buff（id为0）</para>
        /// <para>UseTemplateBuff 是 true时是 表格中的buff</para>
        /// <para>但是表示的都是 将要创建的 buff </para>
        /// </summary>
        public Buff TemporaryBuff;

        public override void OnExecute(OneTrigger trigger, object arg = null)
        {
            if (trigger != null && trigger.CurTarget != null)
            {
                //对当前 target 创建一个buff
                if (UseTemplateBuff != null)
                {
                    var newBuff = new Buff();
                    TemporaryBuff.Clone(newBuff);
                    newBuff.Caster = trigger.ParentTriggerList.ParentSkill.Caster;
                    newBuff.ParentSkill = trigger.ParentTriggerList.ParentSkill;
                    trigger.CurTarget.AddBuff(newBuff);
                }
            }
        }
        public override void Clone(CommandBase copy)
        {
            if (copy is CommandCreateBuff copyCreateBuff)
            {
                copyCreateBuff.UseTemplateBuff = UseTemplateBuff;
                BuffID.Clone(copyCreateBuff.BuffID);
                
            }
        }

        public override void ReadFromFile(BinaryReader reader)
        {
            UseTemplateBuff = reader.ReadBoolean();
            if (UseTemplateBuff)
            {
                BuffID.ReadFromFile(reader);
            }
            else
            {
                TemporaryBuff = SkillFactory.CreateNewBuff();
                TemporaryBuff.ReadFromFile(reader);
            }
        }

        public override void WriteToFile(BinaryWriter writer)
        {
            writer.Write(UseTemplateBuff);
            if (UseTemplateBuff)
            {
                BuffID.WriteToFile(writer);
            }
            else
            {
                if (TemporaryBuff != null)
                {
                    TemporaryBuff.WriteToFile(writer);
                }
                else
                {
                    Debug.LogError("Not UseTemplate but No TemporaryBuff");
                }
            }
        }

        public override void SetSkillValue(DataRowBase dataTable)
        {
            if (UseTemplateBuff)
            {
                BuffID.SetSkillValue(dataTable);
            }
        }
    }
}