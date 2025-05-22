using Unity.Entities;
using UnityEngine;

public class BulletSpawnerAuthoring : MonoBehaviour
{
    public GameObject BulletPrefab;
    
    class Baker : Baker<BulletSpawnerAuthoring>
    {
        public override void Bake(BulletSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            // 转换子弹预制体
            var prefabEntity = GetEntity(authoring.BulletPrefab, TransformUsageFlags.Dynamic);
            
            // 添加生成器组件
            AddComponent(entity, new BulletSpawnerComponent
            {
                BulletPrefabEntity = prefabEntity
            });
        }
    }
}