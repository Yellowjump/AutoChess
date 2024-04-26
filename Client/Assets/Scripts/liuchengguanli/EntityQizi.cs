using GameFramework.Fsm;
using System;
using System.Collections;
using System.Collections.Generic;
using DataTable;
using SkillSystem;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace liuchengguanli
{
    public class EntityQizi : EntityBase
    {
        public int level;
        public int money;
        public int ID;//hero表中ID
        public TriggerList NormalSkill;
        public TriggerList SpSkill;
        public float xueliangsum=1000;//��Ѫ��
        public float powersum =100;//����
        public float xueliangnow;//��ǰѪ��
        public float powernow;//��ǰ��
        public float x;
        public float y;//�������ϵ�λ��
        public float gongjiDistence;//��������
        public Slider xuetiao;
        public Slider power;
        public Image levelImage;
        public Animator animator;//����������
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
            QiziGuanLi.Instance.QiziCXList.Add(this);
            xueliangnow = xueliangsum;
            powernow = 0;//��ʼ������
            gongjiDistence = 1.2f;//��ʼ����������
            this.xuetiao = this.GObj.transform.Find("xuetiao_qizi/xuetiao").GetComponent<Slider>();
            this.xuetiao.value = this.xueliangnow / this.xueliangnow;
            this.power = this.GObj.transform.Find("xuetiao_qizi/pow").GetComponent<Slider>();
            this.power.value = this.powernow / this.powersum;
            this.levelImage = this.GObj.transform.Find("xuetiao_qizi/level").GetComponent<Image>();
            this.levelImage.sprite = QiziGuanLi.Instance.ListQiziLevelSprite[0];
            this.animator = this.GObj.GetComponent<Animator>();
            //GameEntry.UI.OpenUIForm("Assets/UIPrefab/xuetiao_qizi.prefab", "middle");
            //this.GObj.GetComponent<Fsm_qizi0>().Init();
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
            var skillTable = GameEntry.DataTable.GetDataTable<DRSkill>("Skill"); //先只初始化 normalSkill
            if (!skillTable.HasDataRow(skillID))
            {
                Log.Error($"heroID:{ID} skillID{skillID} invalid no match TableRow");
                return;
            }

            var skillTableData = skillTable[skillID];
            var skillTemplates = GameEntry.DataTable.GetDataTable<DRSkillTemplate>("SkillTemplate");
            if (!skillTemplates.HasDataRow(skillTableData.TemplateID))
            {
                Log.Error($"skillID{skillID} no match Template{skillTableData.TemplateID}");
                return;
            }

            var temp = skillTemplates[skillTableData.TemplateID].Skill;
        }

        public void Remove()
        {
            if (this.GObj.transform.localPosition.z == -4.5f)//��������ڳ���
            {
                QiziGuanLi.Instance.QiziCXList.Remove(this);
                QiziGuanLi.Instance.changxia[(int)this.GObj.transform.localPosition.x + 4] = -1;
            }
            else
            {
                QiziGuanLi.Instance.QiziCSList.Remove(this);
            }
            QiziGuanLi.Instance.QiziList.Remove(this);
            Pool.instance.PoolObject[this.Index].Release(this.GObj);
            Pool.instance.PoolEntity.Release(this);
        }
    }
}
