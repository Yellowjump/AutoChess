using SkillSystem;
using Unity.Entities;
using UnityEngine;
using UnityGameFramework.Runtime;
using Zizouqi.CombatDots;

namespace Entity
{
    public partial class EntityQizi
    {
        private void InitState()
        {
            if (m_CombatUnitHandle == 0) return;
            CombatUnitBrainBridge.SetPhase(m_CombatUnitHandle,
                CombatUnitBrainPhase.Idle);
            AddAnimCommandIdle();
        }

        private void UpdateState(float elapseSeconds, float realElapseSeconds)
        {
            if (m_CombatUnitHandle == 0) return;
            CombatUnitBrainPhase phase =
                CombatUnitBrainBridge.GetPhase(m_CombatUnitHandle);
            if (phase != CombatUnitBrainPhase.Idle &&
                phase != CombatUnitBrainPhase.Moving) return;

            if (phase == CombatUnitBrainPhase.Moving &&
                CombatMovementBridge.ConsumeAdvanced(m_CombatUnitHandle,
                    elapseSeconds, out bool arrived, out _, out _, out _) &&
                !arrived) return;

            if (phase == CombatUnitBrainPhase.Idle &&
                (!HotGameEntry.HeroManager.InBattle ||
                 !CombatBoardBridge.FindOccupant(HeroUID, out _, out _)))
                return;
            if (!CombatUnitBrainBridge.ShouldEvaluate(m_CombatUnitHandle))
                return;

            DynamicBuffer<CombatUnitBrainCandidate> candidates =
                CombatUnitBrainBridge.GetCandidates(m_CombatUnitHandle);
            for (int i = 0; i < candidates.Length; i++)
            {
                CombatUnitBrainCandidate candidate = candidates[i];
                EntityQizi target = candidate.TargetUid == 0 ? null :
                    HotGameEntry.HeroManager.GetEntityByUID(candidate.TargetUid);
                if (candidate.TargetUid != 0 && target == null) continue;
                if (candidate.Result == (int)CheckCastSkillResult.CanCast)
                {
                    CurAttackTarget = target;
                    CastNormalSkill(candidate.SkillIndex);
                    if (target != null)
                    {
                        Vector3 targetPosition = target.LogicPosition;
                        CombatAttributeBridge.QueueFacing(m_CombatUnitHandle,
                            targetPosition.x, targetPosition.y,
                            targetPosition.z);
                    }
                    if (phase == CombatUnitBrainPhase.Moving)
                        CombatMovementBridge.End(m_CombatUnitHandle);
                    return;
                }
                if (candidate.Result ==
                    (int)CheckCastSkillResult.TargetOutRange && target != null)
                {
                    Vector2Int from = HotGameEntry.HeroManager.GetIndexQizi(this);
                    Vector2Int to = HotGameEntry.HeroManager.GetIndexQizi(target);
                    bool snapshotValid = candidate.PathFound &&
                        candidate.StartRow == from.y &&
                        candidate.StartColumn == from.x &&
                        candidate.TargetRow == to.y &&
                        candidate.TargetColumn == to.x &&
                        (candidate.NextRow == from.y &&
                         candidate.NextColumn == from.x ||
                         CombatBoardBridge.GetOccupant(candidate.NextRow,
                             candidate.NextColumn) == -1);
                    Vector2Int next = snapshotValid
                        ? new Vector2Int(candidate.NextColumn,
                            candidate.NextRow)
                        : HotGameEntry.HeroManager.Findpath(from, to,
                            CombatSkillSlotBridge.GetNormalRange(
                                m_CombatUnitHandle, candidate.SkillIndex));
                    if (next.x < 0 || next.y < 0 ||
                        (phase == CombatUnitBrainPhase.Moving && next == from))
                        continue;
                    CurAttackTarget = target;
                    if (phase == CombatUnitBrainPhase.Idle)
                    {
                        CombatMovementBridge.End(m_CombatUnitHandle);
                        CombatUnitBrainBridge.SetPhase(m_CombatUnitHandle,
                            CombatUnitBrainPhase.Moving);
                        return;
                    }
                    Vector3 destination = HotGameEntry.HeroManager.GetGeziPos(
                        next.y, next.x);
                    CombatMovementBridge.Begin(m_CombatUnitHandle,
                        destination.x, destination.y, destination.z);
                    AddAnimCommandRun();
                    CombatAttributeBridge.QueueFacing(m_CombatUnitHandle,
                        destination.x, destination.y, destination.z);
                    CombatBoardBridge.MoveOccupant(from.y, from.x,
                        next.y, next.x, HeroUID);
                    return;
                }
                if (phase == CombatUnitBrainPhase.Idle &&
                    candidate.Result ==
                    (int)CheckCastSkillResult.NormalAtkWait)
                    CurAttackTarget = target;
            }
            if (phase == CombatUnitBrainPhase.Moving)
                ChangeToIdleState();
        }

        private void ChangeToIdleState()
        {
            if (m_CombatUnitHandle == 0) return;
            CombatAttackCastBridge.End(m_CombatUnitHandle);
            CombatMovementBridge.End(m_CombatUnitHandle);
            CombatUnitBrainBridge.SetPhase(m_CombatUnitHandle,
                CombatUnitBrainPhase.Idle);
            AddAnimCommandIdle();
        }

        private void ChangeToWinState()
        {
            if (m_CombatUnitHandle == 0) return;
            CombatAttackCastBridge.End(m_CombatUnitHandle);
            CombatMovementBridge.End(m_CombatUnitHandle);
            CombatUnitBrainBridge.SetPhase(m_CombatUnitHandle,
                CombatUnitBrainPhase.BattleWon);
            AddAnimCommandWin();
        }

        private void ChangeToLoseState()
        {
            if (m_CombatUnitHandle == 0) return;
            CombatAttackCastBridge.End(m_CombatUnitHandle);
            CombatMovementBridge.End(m_CombatUnitHandle);
            CombatUnitBrainBridge.SetPhase(m_CombatUnitHandle,
                CombatUnitBrainPhase.BattleLost);
            AddAnimCommandLose();
        }

        private void DestoryState()
        {
            if (m_CombatUnitHandle == 0) return;
            CombatAttackCastBridge.End(m_CombatUnitHandle);
            CombatMovementBridge.End(m_CombatUnitHandle);
        }
    }
}
