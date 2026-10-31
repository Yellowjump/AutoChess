using System;
using UnityEngine;

namespace Zizouqi.CombatDots
{
    public static class CombatSpawnPlacement
    {
        public static Vector3 ResolveProjectile(CombatCommandNode node,
            Vector3 ownerPosition, Vector3 targetPosition)
        {
            Vector3 forward = targetPosition - ownerPosition;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            return ownerPosition + rotation * new Vector3(node.ProjectileOffsetX,
                node.ProjectileOffsetY, node.ProjectileOffsetZ);
        }

        public static Vector3 ResolvePositionPoint(CombatCommandNode node,
            Vector3 ownerPosition, Vector3 targetPosition,
            bool hasOwner, int targetCamp,
            Func<double, double, double> rollOffset)
        {
            Vector3 offset = new Vector3(node.PositionPointOffsetX,
                node.PositionPointOffsetY, node.PositionPointOffsetZ);
            Vector3 maximum = new Vector3(node.PositionPointOffsetMaxX,
                node.PositionPointOffsetMaxY, node.PositionPointOffsetMaxZ);
            if (maximum != Vector3.zero)
            {
                offset = new Vector3(
                    (float)rollOffset(node.PositionPointOffsetX, maximum.x),
                    (float)rollOffset(node.PositionPointOffsetY, maximum.y),
                    (float)rollOffset(node.PositionPointOffsetZ, maximum.z));
            }
            switch (node.PositionPointMode)
            {
                case 0: // PosPointPositionType.OwnerOffset
                    return ResolveOffset(ownerPosition, targetPosition, offset);
                case 1: // PosPointPositionType.TargetOffset
                    return hasOwner
                        ? ResolveOffset(targetPosition, ownerPosition, offset)
                        : targetPosition + offset;
                case 2: // PosPointPositionType.CrowdedPos
                    return CombatPositionPointPlacement.FindCrowdedPosition(targetCamp,
                        offset.x / 1000f);
                default:
                    return Vector3.zero;
            }
        }

        private static Vector3 ResolveOffset(Vector3 sourcePosition,
            Vector3 lookAtPosition, Vector3 offset)
        {
            Vector3 forward = lookAtPosition - sourcePosition;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            return sourcePosition + rotation * offset;
        }
    }
}
