using System.Collections.Generic;
using DataTable;
using GameFramework;
using GameFramework.Fsm;
using UnityGameFramework.Runtime;

namespace Entity
{
    public partial class EntityQizi
    {
        private List<SfxEntity> SfxList = new List<SfxEntity>();
        public void AddSfx(int sfxID)
        {
            var sfxTable = GameEntry.DataTable.GetDataTable<DRSfx>("Sfx");
            if (sfxTable.HasDataRow(sfxID))
            {
                var sfxData = sfxTable[sfxID];
                SfxEntity oneNewSfx = null;
                if (sfxData.IsOnlyOne)
                {
                    oneNewSfx = SfxList.Find(entity => entity.SfxID == sfxID);
                }

                if (oneNewSfx == null)
                {
                    oneNewSfx = ReferencePool.Acquire<SfxEntity>();
                    oneNewSfx.SfxID = sfxID;
                    oneNewSfx.Owner = this;
                    oneNewSfx.PosOffset = sfxData.PosOffset;
                    oneNewSfx.DurationMs = sfxData.DurationMs;
                    oneNewSfx.InitGObj();
                    SfxList.Add(oneNewSfx);
                }
                oneNewSfx.ExistNum++;
            }
        }

        public void RemoveSfx(int sfxID)
        {
            var oneSfx = SfxList.Find(entity => entity.SfxID == sfxID);
            oneSfx.ExistNum--;
            if (oneSfx.ExistNum <= 0)
            {
                ReferencePool.Release(oneSfx);
                SfxList.Remove(oneSfx);
            }
        }

        private void RemoveAllSfx()
        {
            foreach (var oneSfx in SfxList)
            {
                ReferencePool.Release(oneSfx);
            }
            SfxList.Clear();
        }
    }
}