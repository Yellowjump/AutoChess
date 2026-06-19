using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityGameFramework.Runtime;

namespace GameMain.Scripts.JobSystem
{
    [BurstCompile]
    public struct TrackingBulletFlyJob: IJobParallelFor
    {
        public float ElapsedTime;
        public NativeArray<HeroComponent.BulletData> Bullets;

        public void Execute(int index)
        {
            var bullet = Bullets[index];

            float3 dir = bullet.TargetPosition - bullet.LogicPosition;
            float dist = math.length(dir);
            float moveDist = bullet.MoveSpeed * ElapsedTime;

            if (dist < moveDist)
            {
                bullet.LogicPosition = bullet.TargetPosition;
                bullet.HasHit = 1; // 命中
            }
            else
            {
                float3 dirNorm = math.normalizesafe(dir);
                bullet.LogicPosition += dirNorm * moveDist;
            }

            Bullets[index] = bullet;
        }
    }
}