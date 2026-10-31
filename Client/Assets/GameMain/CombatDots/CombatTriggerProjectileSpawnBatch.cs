using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Zizouqi.CombatDots
{
    public struct CombatTriggerProjectileSpawnInput
    {
        public float3 OwnerPosition;
        public float3 TargetPosition;
        public float3 Offset;
        public int DamageMode;
        public int DamageBaseValue;
        public int DamageFixedValue;
        public int DamagePercent;
        public bool IsDamage;
        public bool HasCaster;
    }

    public struct CombatTriggerProjectileSpawnResult
    {
        public float3 Position;
        public float Damage;
    }

    // Only immutable placement data enters the worker threads. Entity creation and
    // trigger side effects are committed later in the original event order.
    public static class CombatTriggerProjectileSpawnBatch
    {
        [BurstCompile]
        private struct PrepareJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CombatTriggerProjectileSpawnInput> Inputs;
            [WriteOnly] public NativeArray<CombatTriggerProjectileSpawnResult> Results;

            public void Execute(int index)
            {
                Results[index] = Evaluate(Inputs[index]);
            }
        }

        private static CombatTriggerProjectileSpawnResult Evaluate(
            CombatTriggerProjectileSpawnInput input)
        {
            if (input.IsDamage)
            {
                return new CombatTriggerProjectileSpawnResult
                {
                    Damage = !input.HasCaster ? 0 :
                        input.DamageMode == 0 ? input.DamageBaseValue :
                        input.DamageMode == 1 ? input.DamageFixedValue +
                            (input.DamagePercent > 0
                                ? input.DamageBaseValue * input.DamagePercent / 100
                                : 0) : 0
                };
            }
            float3 forward = input.TargetPosition - input.OwnerPosition;
            quaternion rotation = quaternion.LookRotationSafe(forward, new float3(0f, 1f, 0f));
            return new CombatTriggerProjectileSpawnResult
            {
                Position = input.OwnerPosition + math.rotate(rotation, input.Offset)
            };
        }

        public static CombatTriggerProjectileSpawnResult PrepareSingle(
            CombatTriggerProjectileSpawnInput input)
        {
            return Evaluate(input);
        }

        public static void Prepare(NativeArray<CombatTriggerProjectileSpawnInput> inputs,
            NativeArray<CombatTriggerProjectileSpawnResult> results)
        {
            new PrepareJob { Inputs = inputs, Results = results }
                .Schedule(inputs.Length, 32).Complete();
        }
    }
}
