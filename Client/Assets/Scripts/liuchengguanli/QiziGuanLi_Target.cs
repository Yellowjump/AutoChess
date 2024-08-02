using System.Collections.Generic;
using Entity;
using GameFramework;
using SkillSystem;
using UnityEngine.Pool;


public partial class QiziGuanLi
{
    public bool GetNearestTarget(EntityQizi source, CampType targetCamp, out EntityQizi target)
    {
        target = null;
        List<EntityQizi> waitCheckList = ListPool<EntityQizi>.Get();
        if ((source.BelongCamp == CampType.Friend&&targetCamp == CampType.Enemy)||(source.BelongCamp == CampType.Enemy&&targetCamp == CampType.Friend)||targetCamp == CampType.Both)
        {
            waitCheckList.AddRange(DirenList);
        }

        if ((source.BelongCamp == CampType.Friend && targetCamp == CampType.Friend) || (source.BelongCamp == CampType.Enemy && targetCamp == CampType.Enemy) || targetCamp == CampType.Both)
        {
            waitCheckList.AddRange(QiziCSList);
        }
        float minDistanceSquare = float.MaxValue;
        foreach (var oneQizi in waitCheckList)
        {
            if (oneQizi.IsValid == false)
            {
                continue;
            }
            var newDistanceSquare = oneQizi.GetDistanceSquare(source);
            if (newDistanceSquare< minDistanceSquare)
            {
                target = oneQizi;
                minDistanceSquare = newDistanceSquare;
            }
        }
        if (Utility.TruncateFloat(minDistanceSquare,4) <source.gongjiDistence*source.gongjiDistence)
        {
            return true;
        }
        return false;
    }
}
