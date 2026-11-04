using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DataTable;
using Cinemachine;
using Entity;
using SkillSystem;
using UnityEngine;
using WorldTimeline;

namespace UnityGameFramework.Runtime
{
    public sealed partial class HeroComponent
    {
        public IEnumerator RunTimelineBattle(IReadOnlyList<ActorState> first, IReadOnlyList<ActorState> second,
            Func<ActorState, int> heroId, Func<ActorState, List<int>> items, int seed, bool visible,
            Action<int[]> completed)
        {
            if (IsTimelineBattle || InBattle || QiziCSList.Count != 0 || DirenList.Count != 0
                || BulletList.Count != 0 || PosPointList.Count != 0)
                throw new InvalidOperationException("自走棋棋盘正在使用，无法开始轮回战斗。");
            if (first.Count == 0 || second.Count == 0 || first.Count > 28 || second.Count > 28)
                throw new InvalidOperationException("轮回战斗双方人数必须在 1 到 28 之间。");

            var participants = first.Concat(second).ToArray();
            var heroes = participants.Select(heroId).ToArray();
            var equipment = participants.Select(items).ToArray();
            var heroTable = GameEntry.DataTable.GetDataTable<DRHero>("Hero");
            var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
            for (int i = 0; i < participants.Length; i++)
            {
                if (!heroTable.HasDataRow(heroes[i]) || equipment[i].Any(id => !itemTable.HasDataRow(id)))
                    throw new InvalidOperationException("轮回角色或装备未映射到有效的自走棋配置。");
            }

            int uid = QiziCurUniqueIndex, itemUid = ItemUniqueIndex;
            int phase = dangqianliucheng;
            var cells = qige.Select(row => (int[])row.Clone()).ToArray();
            var entities = new List<EntityQizi>();
            bool gridVisible = m_QigeRoot.gameObject.activeSelf;
            Vector3 offset = _qigePosOffset;
            var random = new System.Random(seed);
            GameObject cameraObject = null;
            Camera previousCanvasCamera = m_InstanceWorldCanvas.worldCamera;
            IsTimelineBattle = true;
            IsTimelineSimulation = !visible;
            try
            {
                if (visible)
                {
                    MoveQigeRoot(GetTerrainMatchHeight(offset));
                    ShowQige(true);
                    var cameraSettings = GameEntry.DataTable.GetDataTable<DRAreaPoint>("AreaPoint")
                        .GetAllDataRows().OrderBy(point => point.Id).First(point => point.AreaPointType == 2);
                    cameraObject = new GameObject("轮回战斗虚拟相机");
                    var camera = cameraObject.AddComponent<CinemachineVirtualCamera>();
                    camera.Priority = 200;
                    camera.m_Lens.ModeOverride = LensSettings.OverrideModes.Perspective;
                    camera.m_Lens.FieldOfView = 60;
                    camera.m_Lens.NearClipPlane = .3f;
                    camera.m_Lens.FarClipPlane = 2000;
                    camera.transform.position = _qigePosOffset + cameraSettings.CameraPosRelate;
                    var rotation = cameraSettings.CameraRotate;
                    camera.transform.rotation = new Quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
                    m_InstanceWorldCanvas.worldCamera = Camera.main;
                }
                FreshQige();
                using (GameFramework.Utility.Random.UseGenerator(random))
                {
                    for (int i = 0; i < participants.Length; i++)
                    {
                        bool friend = i < first.Count;
                        int slot = friend ? i : i - first.Count;
                        var position = friend ? GetEmptyFriendPos() : new Vector2Int(slot % 7, 7 - slot / 7);
                        int row = position.y;
                        int column = position.x;
                        var entity = GetNewEntityQizi();
                        entities.Add(entity);
                        entity.BelongCamp = friend ? CampType.Friend : CampType.Enemy;
                        entity.NextCastNormalSkillIndex = 0;
                        entity.CurCastNormalSkillIndex = 0;
                        entity.CurAttackTarget = null;
                        entity.Init(heroes[i], equipment[i]);
                        entity.rowIndex = row;
                        entity.columnIndex = column;
                        entity.SavePos = new Vector2Int(column, row);
                        (friend ? QiziCSList : DirenList).Add(entity);
                        qige[row][column] = entity.HeroUID;
                        entity.LogicPosition = GetGeziPos(row, column);
                        var actor = participants[i];
                        entity.GetAttribute(AttributeType.MaxHp).SetBaseNum(actor.MaxHealth);
                        entity.GetAttribute(AttributeType.Hp).SetBaseNum(actor.Health);
                        entity.GetAttribute(AttributeType.AttackDamage).SetBaseNum(actor.Attack);
                    }
                }
                if (visible)
                {
                    foreach (var entity in entities) entity.InitGObj();
                    float deadline = Time.realtimeSinceStartup + 15;
                    while (entities.Any(entity => entity.GObj == null))
                    {
                        if (Time.realtimeSinceStartup >= deadline)
                            throw new InvalidOperationException("战斗角色模型加载超时。");
                        yield return null;
                    }
                }
                StartBattle();
                float step = GameEntry.LogicDeltaTime;
                if (step <= 0 || float.IsNaN(step) || float.IsInfinity(step))
                    throw new InvalidOperationException("战斗逻辑步长无效。");
                int ticks = Mathf.CeilToInt(ConstValue.BattleMaxDuration / step);
                float accumulator = 0;
                int tick = 0;
                while (tick < ticks && QiziCSList.Any(entity => entity.IsValid) && DirenList.Any(entity => entity.IsValid))
                {
                    int steps = ticks - tick;
                    if (visible)
                    {
                        yield return null;
                        accumulator += Time.deltaTime;
                        steps = Math.Min(steps, Mathf.FloorToInt(accumulator / step));
                        accumulator -= steps * step;
                    }
                    for (int i = 0; i < steps; i++)
                    {
                        using (GameFramework.Utility.Random.UseGenerator(random))
                            OnLogicUpdate(step, step);
                        tick++;
                        if (!QiziCSList.Any(entity => entity.IsValid) || !DirenList.Any(entity => entity.IsValid)) break;
                    }
                }
                var health = entities.Select(entity => entity.IsValid
                    ? Math.Max(0, (int)entity.GetAttribute(AttributeType.Hp).GetFinalValue()) : 0).ToArray();
                // Match the original game's battle timeout: the attacking side loses.
                if (health.Take(first.Count).Any(hp => hp > 0) && health.Skip(first.Count).Any(hp => hp > 0))
                {
                    Array.Clear(health, 0, first.Count);
                    Log.Warning("轮回自走棋战斗超时，按进攻方失败结算。");
                }
                if (health.All(hp => hp == 0))
                {
                    // Resolve a simultaneous wipe in favour of the defending side.
                    health[first.Count] = 1;
                }
                if (visible)
                {
                    foreach (var entity in entities.Where(entity => entity.IsValid)) entity.OnWinBattle();
                    float until = Time.realtimeSinceStartup + 1;
                    while (Time.realtimeSinceStartup < until)
                    {
                        UpdateNoBattle(Time.deltaTime, Time.unscaledDeltaTime);
                        yield return null;
                    }
                }
                completed(health);
            }
            finally
            {
                try
                {
                    ClearBullet();
                    ClearPosPoint();
                    foreach (var entity in entities) entity.Remove();
                    QiziCSList.Clear();
                    DirenList.Clear();
                    for (int row = 0; row < cells.Length; row++) Array.Copy(cells[row], qige[row], cells[row].Length);
                    if (visible) ClearAllDamageNum();
                }
                finally
                {
                    QiziCurUniqueIndex = uid;
                    ItemUniqueIndex = itemUid;
                    dangqianliucheng = phase;
                    IsTimelineSimulation = false;
                    IsTimelineBattle = false;
                    if (cameraObject != null)
                    {
                        cameraObject.SetActive(false);
                        Destroy(cameraObject);
                    }
                    m_InstanceWorldCanvas.worldCamera = previousCanvasCamera;
                    MoveQigeRoot(offset);
                    ShowQige(gridVisible);
                }
            }

        }
    }
}
