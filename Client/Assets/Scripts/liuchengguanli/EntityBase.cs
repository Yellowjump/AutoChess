using UnityEngine;
using UnityGameFramework.Runtime;

namespace liuchengguanli
{
    /// <summary>
    /// 所有在棋盘上有的GameObject的管理实体 基类，包括英雄，子弹，炮台，召唤物
    /// </summary>
    public class EntityBase
    {
        public int Index;//保存Object的编号
        public GameObject GObj;
        public virtual void Init(int index)
        {
            Log.Info("hfk:base");
        }
    }
}