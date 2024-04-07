using System.Collections;
using System.Collections.Generic;
using DataTable;
using SkillSystem;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace liuchengguanli
{
    public class EntityQizi : EntityBase
    {
        public int level;
        public int money;
        public int ID;//hero表中ID
        public override void Init(int i)
        {
            this.Index = i;
            this.level = 1;
            this.money = QiziGuanLi.Instance.qizi[i];
            this.GObj = Pool.instance.PoolObject[i].Get();
            this.GObj.transform.localScale = Vector3.one;
            QiziGuanLi.Instance.QiziList.Add(this);
            ID = 1;//todo 后续 从棋子购买处获取ID
            InitSkill();
            //Log.Info("hfk:qizichushihua:" + this.GObj.name+"list.size: " + Pool.instance.list.Count + "list[0]position:" + Pool.instance.list[0].GObj.transform.localPosition);
        }

        private void InitSkill()
        {
            var heroTable = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            if (!heroTable.HasDataRow(ID))
            {
                Log.Error($"heroID:{ID} invalid no match TableRow");
                return;
            }
            var skillID = heroTable[ID].SkillID;
            var skillTable = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
            if (!skillTable.HasDataRow(skillID))
            {
                Log.Error($"heroID:{ID} skillID{skillID} invalid no match TableRow");
                return;
            }
            var skillTableData = skillTable[skillID];
            
        }
    }
}
