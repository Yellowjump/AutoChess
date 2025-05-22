using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using GameFramework;
using GameFramework.Resource;
using SkillSystem;
using UnityEntity = Unity.Entities.Entity;
using Entity;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        private EntityManager entityManager;
        private BeginSimulationEntityCommandBufferSystem commandBufferSystem;
        private bool isDamageNumberInitialized;
        private GameObject damageNumberPrefab;
        public UnityEntity damageNumberPrefabEntity;

        private void InitializeDamageNumberSystem()
        {
            if (isDamageNumberInitialized) return;

            // 获取ECS系统
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            commandBufferSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            
            // 加载预制体
            var loadAssetCallbacks = new LoadAssetCallbacks(OnLoadDamageNumberPrefabSuccess);
            //GameEntry.Resource.LoadAsset("Assets/GameMain/Prefabs/UI/DamageNumber.prefab", loadAssetCallbacks);
        }

        private void OnLoadDamageNumberPrefabSuccess(string assetName, object asset, float duration, object userData)
        {
            damageNumberPrefab = asset as GameObject;
            if (damageNumberPrefab == null)
            {
                Log.Error("DamageNumber prefab is not a GameObject!");
                return;
            }
            //damageNumberPrefabEntity = GetEntity(world, settings, damageNumberPrefab);
            isDamageNumberInitialized = true;
        }

        private void OnLoadDamageNumberPrefabFailure(string assetName, LoadResourceStatus status, string errorMessage)
        {
            Log.Error($"Failed to load DamageNumber prefab! Status: {status}, Error: {errorMessage}");
        }

        public void ShowDamageNumEntity(CauseDamageData damageData)
        {
            if (!isDamageNumberInitialized)
            {
                Log.Warning("DamageNumber system not initialized yet!");
                return;
            }

            // 创建命令缓冲区
            var ecb = commandBufferSystem.CreateCommandBuffer();
            
            // 创建伤害数字实体
            var entity = ecb.CreateEntity();
            
            // 添加组件
            ecb.AddComponent(entity, new DamageNumberComponent
            {
                DamageValue = (int)damageData.DamageValue,
                AnimationDuration = 1f,
                CurrentDuration = 0,
                TargetFontSize = 20,
                CurrentFontSize = 40,
                TargetPosition = damageData.Target.LogicHitPosition,
                OffsetX = 2f
            });
            
            // 创建显示实体（从预制体实例化）
            var displayEntity = ecb.Instantiate(damageNumberPrefabEntity);
            
            // 设置位置
            ecb.SetComponent(displayEntity, LocalTransform.FromPosition(damageData.Target.LogicHitPosition));
            
            // 设置显示组件
            ecb.AddComponent(entity, new DamageNumberDisplayComponent
            {
                DisplayEntity = displayEntity
            });
            
            // 添加TextMeshPro组件
            ecb.AddComponent(entity, new TextMeshProComponent
            {
                DamageValue = (int)damageData.DamageValue
            });
        }
        public void FireBullet(EntityQizi caster, float3 position, float3 direction, float damage)
        {
            var request = new BulletSpawnRequestComponent
            {
                Position = position,
                Direction = direction,
                Damage = damage,
                CasterUID = caster.HeroUID,
            };

            var ecb = commandBufferSystem.CreateCommandBuffer();
            var requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, request);
        }
    }

    // 伤害数字组件
    public struct DamageNumberComponent : IComponentData
    {
        public int DamageValue;        // 伤害数值
        public float AnimationDuration; // 动画持续时间
        public float CurrentDuration;   // 当前动画时间
        public float TargetFontSize;    // 目标字体大小
        public float CurrentFontSize;   // 当前字体大小
        public float3 TargetPosition;   // 目标位置
        public float OffsetX;          // X轴偏移
    }

    // 伤害数字显示组件
    public struct DamageNumberDisplayComponent : IComponentData
    {
        public UnityEntity DisplayEntity;    // 关联的显示实体
    }

    // TextMeshPro组件
    public struct TextMeshProComponent : IComponentData
    {
        public int DamageValue;    // 伤害数值
    }
} 