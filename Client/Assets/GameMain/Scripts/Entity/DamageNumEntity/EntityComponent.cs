using Unity.Entities;
using UnityEntity = Unity.Entities.Entity;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
// 子弹组件
public struct BulletComponent : IComponentData
{
    public float Speed;           // 子弹速度
    public float Damage;         // 子弹伤害
    public float LifeTime;       // 子弹生命周期
    public float CurrentLifeTime; // 当前存活时间
    public int CasterUID;        // 发射者
    public float3 Direction;     // 子弹方向
    public float3 StartPosition; // 起始位置
    public float3 Position;      // 生成位置
    public float Radius;         // 子弹半径(用于碰撞检测)
}

// 子弹生成器组件
public struct BulletSpawnerComponent : IComponentData
{
    public UnityEntity BulletPrefabEntity;  // 子弹预制体
    public float SpawnInterval;  // 生成间隔
    public float LastSpawnTime;  // 上次生成时间
}

// 子弹生成请求组件(用于记录需要生成的子弹)
public struct BulletSpawnRequestComponent : IComponentData
{
    public float3 Position;      // 生成位置
    public float3 Direction;     // 发射方向
    public float Damage;         // 伤害值
    public int CasterUID;        // 发射者
}
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class BulletSpawnerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
            
        foreach (var (request, entity) in 
            SystemAPI.Query<RefRO<BulletSpawnRequestComponent>>().WithEntityAccess())
        {
            var prefabEntity = SystemAPI.GetComponent<BulletSpawnerComponent>(
                SystemAPI.GetSingletonEntity<BulletSpawnerComponent>()).BulletPrefabEntity;
                
            var bullet = ecb.Instantiate(prefabEntity);
            
            var bulletComponent = new BulletComponent
            {
                Position = request.ValueRO.Position,
                Direction = request.ValueRO.Direction,
                Damage = request.ValueRO.Damage,
                CasterUID = request.ValueRO.CasterUID,
                Speed = 10f,
                LifeTime = 5f,
                CurrentLifeTime = 0,
                Radius = 0.5f
            };
            
            ecb.SetComponent(bullet, bulletComponent);
            ecb.RemoveComponent<BulletSpawnRequestComponent>(entity);
        }
    }
}

public partial struct BulletFlySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        
        foreach (var (bullet, transform, entity) in 
            SystemAPI.Query<RefRW<BulletComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            // 更新生命周期
            bullet.ValueRW.CurrentLifeTime += deltaTime;
            
            // 检查是否超时
            if (bullet.ValueRO.CurrentLifeTime >= bullet.ValueRO.LifeTime)
            {
                SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged)
                    .DestroyEntity(entity);
                continue;
            }
            
            // 更新位置
            var position = bullet.ValueRO.Position + bullet.ValueRO.Direction * bullet.ValueRO.Speed * deltaTime;
            bullet.ValueRW.Position = position;
            
            // 检查碰撞
            CheckCollision(entity, ref bullet.ValueRW, ref state);
        }
    }
    private void CheckCollision(UnityEntity bulletEntity, ref BulletComponent bullet, ref SystemState state)
    {
        // 使用Physics.SphereCast或Physics.OverlapSphere进行碰撞检测
        // var hits = Physics.OverlapSphere(bullet.Position, bullet.Radius);
        
        // foreach (var hit in hits)
        // {
        //     // 检查碰撞对象是否是目标
        //     if (IsValidTarget(hit.gameObject, bullet.Caster))
        //     {
        //         // 造成伤害
        //         DealDamage(hit.gameObject, bullet.Damage);
                
        //         // 销毁子弹
        //         SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
        //             .CreateCommandBuffer(state.WorldUnmanaged)
        //             .DestroyEntity(bulletEntity);
        //         break;
        //     }
        // }
    }
}