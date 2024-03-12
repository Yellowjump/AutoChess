using UnityEngine;

namespace liuchengguanli
{
    /// <summary>
    /// 所有在棋盘上有的GameObject的管理实体 基类，包括英雄，子弹，炮台，召唤物
    /// </summary>
    public class EntityBase:MonoBehaviour
    {
        public int Index;//保存Object的编号
        public GameObject GObj;


    }
}