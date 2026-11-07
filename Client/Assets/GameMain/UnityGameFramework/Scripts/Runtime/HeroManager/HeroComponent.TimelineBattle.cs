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
        internal bool IsStationaryTargetPreview { get; private set; }
        internal TimelineBurnVisuals BurnVisuals { get; private set; }
#if UNITY_EDITOR
        private EntityQizi previewBurnTarget;

        internal void SeedPreviewBurnBeforeHit(EntityQizi target, CauseDamageData hit)
        {
            if (previewBurnTarget != target || hit.Caster?.BelongCamp != CampType.Friend ||
                hit.SourceSkill?.IsActiveDamageSkill != true || hit.DamageValue <= 0) return;
            previewBurnTarget = null;
            CommandBurnEffect.FillPreviewBurn(hit.Caster, target);
        }
#endif

        public IEnumerator RunTimelineBattle(IReadOnlyList<ActorState> first, IReadOnlyList<ActorState> second,
            Func<ActorState, int> heroId, Func<ActorState, List<int>> items, int seed, bool visible,
            Action<int[]> completed, int regionDistance = 0, bool unlimitedDuration = false,
            bool seedPreviewBurn = false, Func<ActorState, string> appearance = null, bool enableUndertow = false,
            bool enableSwordRecall = false, Func<ActorState, int> techniqueSkill = null,
            Action<ActorState, int> settleTechnique = null, bool lightningFormation = false,
            ActorState reportOwner = null)
        {
            if (unlimitedDuration && !visible)
                throw new ArgumentException("无时间限制的战斗必须在前台运行。", nameof(unlimitedDuration));
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
            GameObject stoneBoard = null;
            Mesh stoneMesh = null;
            Material stoneMaterial = null;
            int? battleDetailsForm = null;
            Camera sceneCamera = visible ? Camera.main : null;
            CameraClearFlags previousClearFlags = sceneCamera != null ? sceneCamera.clearFlags : default;
            Color previousBackground = sceneCamera != null ? sceneCamera.backgroundColor : default;
            Camera previousCanvasCamera = m_InstanceWorldCanvas.worldCamera;
            IsTimelineBattle = true;
            IsTimelineSimulation = !visible;
            bool stationaryEnemies = unlimitedDuration && second.Count == 3;
            IsStationaryTargetPreview = stationaryEnemies;
            try
            {
                if (visible)
                {
                    if (sceneCamera != null)
                    {
                        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                        sceneCamera.backgroundColor = new Color(.72f, .69f, .63f);
                    }
                    var stoneShader = Resources.Load<Shader>("TimelineBattleStone");
                    if (stoneShader != null && stoneShader.isSupported)
                    {
                        stoneBoard = new GameObject("轮回战斗石台");
                        stoneBoard.transform.position = offset;
                        stoneMesh = BuildTimelineStoneBoard(regionDistance);
                        stoneMaterial = new Material(stoneShader);
                        stoneBoard.AddComponent<MeshFilter>().sharedMesh = stoneMesh;
                        stoneBoard.AddComponent<MeshRenderer>().sharedMaterial = stoneMaterial;
                    }
                    else
                    {
                        Debug.LogWarning("TimelineBattleStone shader is missing or unsupported. Falling back to the original battle grid.", this);
                    }
                    ShowQige(stoneBoard == null);
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
                    BurnVisuals = cameraObject.AddComponent<TimelineBurnVisuals>();
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
                        // Keep the primary target closest to the player's starting cell.
                        if (!friend && lightningFormation)
                            position = new Vector2Int(slot * 2, stationaryEnemies ? 3 : 5);
                        else if (!friend && stationaryEnemies)
                            position = new Vector2Int(slot == 2 ? 6 : slot, 3 + (slot == 1 ? 1 : 0));
                        int row = position.y;
                        int column = position.x;
                        var entity = GetNewEntityQizi();
                        entities.Add(entity);
                        entity.BelongCamp = friend ? CampType.Friend : CampType.Enemy;
                        entity.NextCastNormalSkillIndex = 0;
                        entity.CurCastNormalSkillIndex = 0;
                        entity.CurAttackTarget = null;
                        entity.TimelineAppearance = appearance?.Invoke(participants[i]);
                        entity.UsesUndertow = enableUndertow && entity.TimelineAppearance == "WreckBeast";
                        entity.HasSwordRecall = enableSwordRecall &&
                            (equipment[i].Contains(9104) || participants[i].Items.Contains(9104));
                        entity.TechniqueSkillId = techniqueSkill?.Invoke(participants[i]) ?? 0;
                        entity.Init(heroes[i], equipment[i], recordBattleOutput: reportOwner != null);
                        if (reportOwner != null)
                        {
                            foreach (var skill in entity.NormalSkillList.Concat(entity.NoAnimAtkSkillList).Concat(entity.PassiveSkillList))
                                entity.BattleOutput.TryAdd(skill.SkillID, new SkillBattleOutput(skill.SkillID, skill.FromItemID, 0, 0, 0));
                        }
                        entity.rowIndex = row;
                        entity.columnIndex = column;
                        entity.SavePos = new Vector2Int(column, row);
                        (friend ? QiziCSList : DirenList).Add(entity);
                        qige[row][column] = entity.HeroUID;
                        entity.LogicPosition = GetGeziPos(row, column);
                        var actor = participants[i];
                        entity.GetAttribute(AttributeType.MaxHp).SetBaseNum(actor.MaxHealth);
                        entity.GetAttribute(AttributeType.Hp).SetBaseNum(actor.Health);
                        entity.GetAttribute(AttributeType.AttackDamage).SetBaseNum(actor.CombatAttack);
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
                    battleDetailsForm = GameEntry.UI.OpenUIForm(UICtrlName.BattleMainPanel, "middle",
                        BattleMainCtrl.OpenMode.InspectOnly);
                }
#if UNITY_EDITOR
                if (stationaryEnemies && seedPreviewBurn) previewBurnTarget = entities[first.Count];
#endif
                StartBattle();
                float step = GameEntry.LogicDeltaTime;
                if (step <= 0 || float.IsNaN(step) || float.IsInfinity(step))
                    throw new InvalidOperationException("战斗逻辑步长无效。");
                int? remainingTicks = unlimitedDuration ? null : Mathf.CeilToInt(ConstValue.BattleMaxDuration / step);
                float accumulator = 0;
                while ((remainingTicks ?? 1) > 0 &&
                    QiziCSList.Any(entity => entity.IsValid) && DirenList.Any(entity => entity.IsValid))
                {
                    int steps = remainingTicks ?? int.MaxValue;
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
                            OnLogicUpdate(step, step, stationaryEnemies);
                        remainingTicks--;
                        if (!QiziCSList.Any(entity => entity.IsValid) || !DirenList.Any(entity => entity.IsValid)) break;
                    }
                }
                var health = entities.Select(entity => entity.IsValid
                    ? Math.Max(0, (int)entity.GetAttribute(AttributeType.Hp).GetFinalValue()) : 0).ToArray();
                // Match the original game's battle timeout: the attacking side loses.
                if (remainingTicks == 0 &&
                    health.Take(first.Count).Any(hp => hp > 0) && health.Skip(first.Count).Any(hp => hp > 0))
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
                for (int i = 0; settleTechnique != null && i < participants.Length; i++)
                    if (health[i] > 0 && entities[i].TechniqueSkillId != 0)
                        settleTechnique(participants[i], entities[i].TechniqueKills);
                if (visible && !unlimitedDuration)
                    for (int i = 0; i < participants.Length; i++)
                    {
                        var entity = entities[i];
                        participants[i].LastLightningBattle =
                            (entity.LightningApplications, entity.LightningTransfers, entity.LightningJumps);
                    }
                if (reportOwner != null)
                {
                    bool playerAttacks = first.Contains(reportOwner);
                    reportOwner.LastBattleOutput = participants.Select((actor, i) => new ActorBattleOutput
                    {
                        ActorId = actor.Id,
                        Allied = (i < first.Count) == playerAttacks,
                        Skills = entities[i].BattleOutput.Values.OrderBy(row => row.SkillId).ToArray()
                    }).ToArray();
                }
                completed(health);
            }
            finally
            {
                IsStationaryTargetPreview = false;
                BurnVisuals = null;
#if UNITY_EDITOR
                previewBurnTarget = null;
#endif
                try
                {
                    if (battleDetailsForm is int formId &&
                        (GameEntry.UI.HasUIForm(formId) || GameEntry.UI.IsLoadingUIForm(formId)))
                        GameEntry.UI.CloseUIForm(formId);
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
                    if (stoneBoard != null)
                    {
                        stoneBoard.SetActive(false);
                        Destroy(stoneBoard);
                    }
                    if (stoneMesh != null) Destroy(stoneMesh);
                    if (stoneMaterial != null) Destroy(stoneMaterial);
                    if (sceneCamera != null)
                    {
                        sceneCamera.clearFlags = previousClearFlags;
                        sceneCamera.backgroundColor = previousBackground;
                    }
                    m_InstanceWorldCanvas.worldCamera = previousCanvasCamera;
                    MoveQigeRoot(offset);
                    ShowQige(gridVisible);
                }
            }

        }

        private Mesh BuildTimelineStoneBoard(int regionDistance)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var paleStone = new Color(.65f, .60f, .51f);
            var darkStone = new Color(.28f, .27f, .23f);
            var groundColor = regionDistance >= 7 ? new Color(.43f, .46f, .34f)
                : regionDistance >= 4 ? new Color(.43f, .44f, .42f)
                : new Color(.44f, .38f, .34f);
            // Leave a flat clearing under the playable cells and raise the terrain outside it.
            const int groundRadius = 24;
            const int groundWidth = groundRadius * 2 + 1;
            for (int z = -groundRadius; z <= groundRadius; z++)
            for (int x = -groundRadius; x <= groundRadius; x++)
            {
                float edge = Mathf.Clamp01((Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - 5f) / 5f);
                float noise = Mathf.PerlinNoise(x * .31f + 25f, z * .31f + 25f);
                vertices.Add(new Vector3(x, -.43f + edge * noise * 1.2f, z));
                colors.Add(groundColor * (.83f + noise * .31f));
                if (x == groundRadius || z == groundRadius) continue;
                int corner = vertices.Count - 1;
                triangles.AddRange(new[] { corner, corner + groundWidth, corner + 1,
                    corner + 1, corner + groundWidth, corner + groundWidth + 1 });
            }
            for (int row = 0; row < qigepos.Length; row++)
            for (int column = 0; column < qigepos[row].Length; column++)
            {
                Vector3 center = qigepos[row][column];
                int first = vertices.Count;
                float shade = .90f + ((row * 13 + column * 7) % 9) * .017f;
                for (int edge = 0; edge < 6; edge++)
                {
                    float angle = (edge * 60 + 30) * Mathf.Deg2Rad;
                    var corner = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * .566f;
                    vertices.Add(center + corner - Vector3.up * .025f);
                    vertices.Add(center + corner - Vector3.up * .42f);
                    colors.Add(paleStone * shade);
                    colors.Add(darkStone);
                }
                for (int edge = 1; edge < 5; edge++)
                    triangles.AddRange(new[] { first, first + (edge + 1) * 2, first + edge * 2 });
                for (int edge = 0; edge < 6; edge++)
                {
                    int a = first + edge * 2;
                    int b = first + ((edge + 1) % 6) * 2;
                    triangles.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
                }
            }
            var mesh = new Mesh { name = "Timeline battle stone tiles" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }
    }
}
