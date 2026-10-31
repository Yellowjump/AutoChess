using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Zizouqi.CombatDots
{
    public static class CombatPositionPointPlacement
    {
        private struct AngleEvent
        {
            public double Angle;
            public bool IsStart;
        }

        // The authored command selects relative to the friendly camp. Visit enemies
        // before friends, then UIDs in ascending order, as the former roster did.
        public static Vector3 FindCrowdedPosition(int targetCamp, float radius)
        {
            List<Vector3> positions = ListPool<Vector3>.Get();
            try
            {
                if (targetCamp == 0 || targetCamp == 2)
                {
                    AppendGroupPositions(0, positions);
                }
                if (targetCamp == 1 || targetCamp == 2)
                {
                    AppendGroupPositions(1, positions);
                }
                if (positions.Count == 0)
                {
                    return CombatBoardBridge.GetOffset();
                }
                if (positions.Count == 1)
                {
                    return positions[0];
                }
                if (positions.Count == 2)
                {
                    Vector3 first = positions[0];
                    Vector3 second = positions[1];
                    float dx = first.x - second.x;
                    float dz = first.z - second.z;
                    return Mathf.Sqrt(dx * dx + dz * dz) < radius
                        ? (first + second) / 2f : first;
                }

                Vector3 bestCenter = Vector3.zero;
                int bestCount = 0;
                foreach (Vector3 basePoint in positions)
                {
                    List<AngleEvent> events = ListPool<AngleEvent>.Get();
                    try
                    {
                        foreach (Vector3 point in positions)
                        {
                            if (point == basePoint)
                            {
                                continue;
                            }
                            double dx = point.x - basePoint.x;
                            double dz = point.z - basePoint.z;
                            double distanceSquared = dx * dx + dz * dz;
                            if (distanceSquared > 4 * radius * radius)
                            {
                                continue;
                            }
                            double angle = (Math.Atan2(dz, dx) * 180.0 / Math.PI + 360) % 360;
                            double halfAngle = Math.Acos(Math.Sqrt(distanceSquared) /
                                (2 * radius)) * 180.0 / Math.PI;
                            double start = (angle - halfAngle + 360) % 360;
                            double end = (angle + halfAngle) % 360;
                            events.Add(new AngleEvent { Angle = start, IsStart = true });
                            if (start > end)
                            {
                                events.Add(new AngleEvent { Angle = 360, IsStart = false });
                                events.Add(new AngleEvent { Angle = 0, IsStart = true });
                            }
                            events.Add(new AngleEvent { Angle = end, IsStart = false });
                        }
                        events.Sort((left, right) =>
                        {
                            int comparison = left.Angle.CompareTo(right.Angle);
                            return comparison != 0 ? comparison :
                                left.IsStart == right.IsStart ? 0 :
                                left.IsStart ? 1 : -1;
                        });
                        int currentCount = 0;
                        int candidateCount = 1;
                        double bestAngle = 0;
                        foreach (AngleEvent angleEvent in events)
                        {
                            currentCount += angleEvent.IsStart ? 1 : -1;
                            if (currentCount > candidateCount)
                            {
                                candidateCount = currentCount;
                                bestAngle = angleEvent.Angle;
                            }
                        }
                        if (candidateCount > bestCount)
                        {
                            bestCount = candidateCount;
                            double radians = bestAngle * Math.PI / 180.0;
                            bestCenter = basePoint + new Vector3(
                                (float)(Math.Cos(radians) * radius), 0,
                                (float)(Math.Sin(radians) * radius));
                        }
                    }
                    finally
                    {
                        ListPool<AngleEvent>.Release(events);
                    }
                }
                return bestCenter;
            }
            finally
            {
                ListPool<Vector3>.Release(positions);
            }
        }

        private static void AppendGroupPositions(int group, List<Vector3> positions)
        {
            List<int> uids = ListPool<int>.Get();
            try
            {
                CombatAttributeBridge.CollectGroupUids(group, uids);
                foreach (int uid in uids)
                {
                    if (!CombatAttributeBridge.TryGetHandleByUid(uid, out int handle) ||
                        !CombatAttributeBridge.IsAlive(handle) ||
                        !CombatAttributeBridge.IsActiveOnBoard(handle))
                    {
                        continue;
                    }
                    CombatAttributeBridge.GetPosition(handle, out float x, out float y,
                        out float z);
                    positions.Add(new Vector3(x, y, z));
                }
            }
            finally
            {
                ListPool<int>.Release(uids);
            }
        }
    }
}
