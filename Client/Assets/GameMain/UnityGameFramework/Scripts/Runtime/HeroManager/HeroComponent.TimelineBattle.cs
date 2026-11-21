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
    internal enum TimelineArenaStyle { Forest, Cliff, Ritual, Tidegate, Ashridge, IceCave, LavaCave, SaltFlat, SaltChannel }

    public sealed partial class HeroComponent
    {
        internal bool IsStationaryTargetPreview { get; private set; }
        internal TimelineBurnVisuals BurnVisuals { get; private set; }
        internal bool CriticalRules { get; private set; }
        internal float TimelineBattleSeconds { get; private set; }
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

        internal IEnumerator RunTimelineBattle(IReadOnlyList<ActorState> first, IReadOnlyList<ActorState> second,
            Func<ActorState, int> heroId, Func<ActorState, List<int>> items, int seed, bool visible,
            Action<int[]> completed, bool unlimitedDuration = false,
            bool seedPreviewBurn = false, Func<ActorState, string> appearance = null, bool enableUndertow = false,
            bool enableSwordRecall = false, Func<ActorState, int> techniqueSkill = null,
            Action<ActorState, int> settleTechnique = null, bool lightningFormation = false,
            ActorState reportOwner = null, bool slashFormation = false, bool closeGuardFormation = false,
            TimelineArenaStyle arenaStyle = TimelineArenaStyle.Forest, bool staggerStoneGuards = false,
            bool openingGuardianPounce = false, bool suppressNamelessSeal = false, bool saltChannelDrained = false,
            Func<ActorState, int> coastalFormation = null, bool timeSliceSimulation = false,
            TimelineBattleSetup setup = null, bool criticalRules = false, bool ancientSwordWaitsForHit = true, bool shortAncientSwordOpening = true)
        {
            if (unlimitedDuration && !visible)
                throw new ArgumentException("无时间限制的战斗必须在前台运行。", nameof(unlimitedDuration));
            if (setup?.DurationSeconds is float duration && (duration <= 0 || float.IsNaN(duration) || float.IsInfinity(duration)))
                throw new ArgumentException("Encounter duration must be finite and positive.", nameof(setup));
            if (IsTimelineBattle || InBattle || QiziCSList.Count != 0 || DirenList.Count != 0
                || BulletList.Count != 0 || PosPointList.Count != 0)
                throw new InvalidOperationException("自走棋棋盘正在使用，无法开始轮回战斗。");
            if (first.Count == 0 || second.Count == 0 || first.Count > 28 || second.Count > 28)
                throw new InvalidOperationException("轮回战斗双方人数必须在 1 到 28 之间。");

            var participants = first.Concat(second).ToArray();
            var heroes = participants.Select(heroId).ToArray();
            var equipment = participants.Select(items).ToArray();
            for (int i = 0; i < equipment.Length; i++)
            {
                var loadout = equipment[i];
                if (loadout.Contains(TimelineOuterGuardianCast.Clerk) &&
                    !loadout.Contains(TimelineOuterGuardianCast.ClerkBasicAttack))
                    equipment[i] = new List<int>(loadout) { TimelineOuterGuardianCast.ClerkBasicAttack };
            }
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
            Material mistMaterial = null;
            int? battleDetailsForm = null;
            Camera sceneCamera = visible ? Camera.main : null;
            CameraClearFlags previousClearFlags = sceneCamera != null ? sceneCamera.clearFlags : default;
            Color previousBackground = sceneCamera != null ? sceneCamera.backgroundColor : default;
            Camera previousCanvasCamera = m_InstanceWorldCanvas.worldCamera;
            CriticalRules = criticalRules;
            TimelineBattleSeconds = 0f;
            IsTimelineBattle = true;
            IsTimelineSimulation = !visible;
            bool stationaryEnemies = unlimitedDuration && second.Count == 3;
            bool tidegate = arenaStyle == TimelineArenaStyle.Tidegate;
            Camera previousInkCamera = TidegateInkRenderFeature.BattleCamera;
            IsStationaryTargetPreview = stationaryEnemies;
            if (arenaStyle is TimelineArenaStyle.IceCave or TimelineArenaStyle.LavaCave)
                CaveHazards = new TimelineCaveHazards(this);
            try
            {
                if (visible)
                {
                    if (sceneCamera != null)
                    {
                        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                        sceneCamera.backgroundColor = tidegate
                            ? new Color(.75f, .73f, .68f)
                            : new Color(.20f, .23f, .22f);
                    }
                    bool ashridge = arenaStyle == TimelineArenaStyle.Ashridge;
                    var ashShader = ashridge ? Resources.Load<Shader>("TimelineAshridge") : null;
                    bool renderAshridge = ashShader != null && ashShader.isSupported;
                    if (renderAshridge && sceneCamera != null) sceneCamera.backgroundColor = new Color(.35f, .36f, .38f);
                    var stoneShader = renderAshridge ? ashShader : Resources.Load<Shader>("TimelineBattleStone");
                    if (stoneShader != null && stoneShader.isSupported)
                    {
                        stoneBoard = new GameObject("轮回战场 · " + arenaStyle);
                        stoneBoard.transform.position = offset;
                        stoneMaterial = new Material(stoneShader);
                        if (renderAshridge)
                        {
                            var mistShader = Resources.Load<Shader>("TimelineAshridgeMist");
                            if (mistShader != null && mistShader.isSupported) mistMaterial = new Material(mistShader);
                            stoneMesh = TimelineAshridgeArena.Build(mistMaterial != null);
                        }
                        else
                        {
                            stoneMesh = BuildTimelineArena(ashridge ? TimelineArenaStyle.Forest : arenaStyle, saltChannelDrained);
                            stoneMaterial.SetFloat("_ArenaSurfaces", 1f);
                            if (tidegate)
                            {
                                stoneMaterial.SetFloat("_TidegateInk", 1f);
                                TidegateInkRenderFeature.BattleCamera = sceneCamera;
                            }
                        }
                        stoneBoard.AddComponent<MeshFilter>().sharedMesh = stoneMesh;
                        var renderer = stoneBoard.AddComponent<MeshRenderer>();
                        if (mistMaterial != null) renderer.sharedMaterials = new[] { stoneMaterial, mistMaterial };
                        else renderer.sharedMaterial = stoneMaterial;
                    }
                    else
                    {
                        Debug.LogWarning("TimelineBattleStone shader is missing or unsupported. Falling back to the original battle grid.", this);
                    }
                    ShowQige(stoneBoard == null);
                    cameraObject = new GameObject("轮回战斗虚拟相机");
                    var camera = cameraObject.AddComponent<CinemachineVirtualCamera>();
                    camera.Priority = 200;
                    camera.m_Lens.ModeOverride = LensSettings.OverrideModes.Perspective;
                    camera.m_Lens.FieldOfView = 60;
                    camera.m_Lens.NearClipPlane = .3f;
                    camera.m_Lens.FarClipPlane = 2000;
                    if (stoneBoard != null)
                    {
                        var focus = offset + (arenaStyle == TimelineArenaStyle.Ritual
                            ? new Vector3(-.35f, .15f, -.30f)
                            : renderAshridge ? new Vector3(-.52f, .72f, .15f)
                            : closeGuardFormation ? new Vector3(-2.15f, .30f, -1.6f) : new Vector3(-.65f, .30f, -.35f));
                        bool wideFormation = participants.Length > 10;
                        var cameraOffset = renderAshridge
                            ? (wideFormation ? new Vector3(1.15f, 4.95f, -9.45f) : new Vector3(.95f, 2.64f, -8.05f))
                            : wideFormation || arenaStyle == TimelineArenaStyle.Ritual ? new Vector3(1.2f, 8.6f, -7.15f)
                            : arenaStyle == TimelineArenaStyle.Cliff ? new Vector3(2.9f, 5.9f, -5.7f)
                            : new Vector3(1.2f, 5.6f, -6.0f);
                        if (renderAshridge) camera.m_Lens.FieldOfView = 59f;
                        if (tidegate && !wideFormation && !slashFormation)
                        {
                            if (closeGuardFormation) focus = offset + new Vector3(-1.24f, .30f, -1.45f);
                            cameraOffset = closeGuardFormation
                                ? new Vector3(1.55f, 4.18f, -5.85f)
                                : new Vector3(1.55f, 4.78f, -6.35f);
                            camera.m_Lens.FieldOfView = 57f;
                        }
                        if (setup?.ExpeditionView == true)
                        {
                            focus = offset + new Vector3(.5f, .30f, -.9f);
                            cameraOffset = new Vector3(1.2f, 7.6f, -7.8f);
                        }
                        camera.transform.SetPositionAndRotation(focus + cameraOffset,
                            Quaternion.LookRotation(-cameraOffset));
                    }
                    else
                    {
                        var cameraSettings = GameEntry.DataTable.GetDataTable<DRAreaPoint>("AreaPoint")
                            .GetAllDataRows().OrderBy(point => point.Id).First(point => point.AreaPointType == 2);
                        camera.transform.position = offset + cameraSettings.CameraPosRelate;
                        var rotation = cameraSettings.CameraRotate;
                        camera.transform.rotation = new Quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
                    }
                    BurnVisuals = cameraObject.AddComponent<TimelineBurnVisuals>();
                    m_InstanceWorldCanvas.worldCamera = Camera.main;
                }
                FreshQige();
                using (GameFramework.Utility.Random.UseGenerator(random))
                {
                    for (int i = 0; i < participants.Length; i++)
                    {
                        bool friend = i < first.Count;
                        bool coastalGuardian = !friend && coastalFormation != null;
                        int slot = coastalGuardian ? coastalFormation(participants[i]) : friend ? i : i - first.Count;
                        // Seven shooters in the back. Six melee units in front and seven in the rank behind them.
                        var position = friend ? GetEmptyFriendPos() : new Vector2Int(slot % 7, 7 - slot / 7);
                        // Keep the primary target closest to the player's starting cell.
                        if (!friend && slashFormation)
                            position = slot == 2 ? new Vector2Int(6, 6) : new Vector2Int(slot, 1);
                        else if (!friend && closeGuardFormation)
                            position = new Vector2Int(slot, 1);
                        else if (!friend && lightningFormation)
                            position = new Vector2Int(slot * 2, stationaryEnemies ? 3 : 5);
                        else if (!friend && stationaryEnemies)
                            position = new Vector2Int(slot == 2 ? 6 : slot, 3 + (slot == 1 ? 1 : 0));
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
                        entity.ShowsPlayerHitFeedback = visible &&
                            (reportOwner != null ? participants[i].Id == reportOwner.Id : friend);
                        if (coastalGuardian && slot < NodeState.CoastalRangedCount)
                        {
                            // Reach the front line without following the crabs into melee.
                            var saltNeedle = entity.NormalSkillList.First(skill => skill.SkillID == TimelineOuterGuardianCast.Eel);
                            saltNeedle.SkillRange = 8500;
                            saltNeedle.DefaultSkillCDMs = 2550 + slot % 3 * 100;
                            entity.IsCoastalShooter = true;
                            entity.SaltFirstAttackDelayMs = slot * 90;
                        }
                        else if (coastalGuardian) entity.IsCoastalMelee = true;
                        entity.SaltChannelDrained = saltChannelDrained;
                        if (!friend && entity.TimelineAppearance == SaltChannel.CrabAppearance)
                            entity.SaltFirstAttackDelayMs = slot % 2 * 300;
                        entity.NamelessSteleSealed = !friend && suppressNamelessSeal &&
                            entity.TimelineAppearance == TimelineMonsterAnimation.NamelessSteleAppearance;
                        entity.AncientSwordReady = !ancientSwordWaitsForHit;
                        entity.ShortAncientSwordOpening = shortAncientSwordOpening;
                        entity.CanOpenWithPounce = openingGuardianPounce && !friend;
                        if (staggerStoneGuards && !friend && entity.TimelineAppearance == "GateGolem")
                            entity.FirstStoneSmashDelayMs = slot % 2 * 300;
                        if (reportOwner != null)
                        {
                            foreach (var skill in entity.NormalSkillList.Concat(entity.NoAnimAtkSkillList).Concat(entity.PassiveSkillList))
                                entity.BattleOutput.TryAdd(skill.SkillID, new SkillBattleOutput(skill.SkillID, skill.FromItemID, 0, 0, 0));
                        }
                        if (setup?.AdjustPosition != null) position = setup.AdjustPosition(participants[i], position);
                        if (setup?.Position != null)
                        {
                            position = setup.Position(participants[i]);
                            if (position.x < 0 || position.x >= 7 || position.y < 0 || position.y >= 8)
                                throw new InvalidOperationException("Encounter formation is outside the battle board.");
                        }
                        else position = FindPlacement(entity, position);
                        if (!TryPlace(entity, position))
                            throw new InvalidOperationException($"No free board position for hero {entity.HeroID}.");

                        entity.SavePos = position;
                        (friend ? QiziCSList : DirenList).Add(entity);
                        entity.LogicPosition = GetGeziPos(position.y, position.x);
                        var actor = participants[i];
                        entity.GetAttribute(AttributeType.MaxHp).SetBaseNum(actor.MaxHealth);
                        entity.GetAttribute(AttributeType.Hp).SetBaseNum(actor.Health);
                        entity.GetAttribute(AttributeType.AttackDamage).SetBaseNum(actor.CombatAttack);
                        setup?.Configure?.Invoke(actor, entity);
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
                    if (tidegate && stoneBoard != null)
                    {
                        foreach (var fighter in QiziCSList)
                            if (fighter.ShowsPlayerHitFeedback)
                                fighter.GObj.transform.localScale *= 1.10f;
                        BurnVisuals.SetTidegatePresentation(entities, offset.y);
                    }
                    if (setup?.Planning == null)
                        battleDetailsForm = GameEntry.UI.OpenUIForm(UICtrlName.BattleMainPanel, "middle",
                            BattleMainCtrl.OpenMode.InspectOnly);
                }
                if (setup?.Planning != null)
                {
                    yield return setup.Planning(participants, entities);
                    yield break;
                }
#if UNITY_EDITOR
                if (stationaryEnemies && seedPreviewBurn) previewBurnTarget = entities[first.Count];
#endif
                // Death clears the entity's Critical reference, but allies can still consume its marks.
                var reportCritical = reportOwner == null ? null : entities.Select(entity => entity.Critical).ToArray();
                StartBattle();
                float step = GameEntry.LogicDeltaTime;
                if (step <= 0 || float.IsNaN(step) || float.IsInfinity(step))
                    throw new InvalidOperationException("战斗逻辑步长无效。");
                int? remainingTicks = unlimitedDuration ? null : Mathf.CeilToInt((setup?.DurationSeconds ?? ConstValue.BattleMaxDuration) / step);
                float accumulator = 0;
                long sliceBudget = System.Diagnostics.Stopwatch.Frequency / 500; // Two milliseconds of combat ticks per frame.
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
                    else if (timeSliceSimulation)
                        steps = Math.Min(steps, 32);
                    long sliceStart = timeSliceSimulation ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                    for (int i = 0; i < steps; i++)
                    {
                        using (GameFramework.Utility.Random.UseGenerator(random))
                        {
                            TimelineBattleSeconds += step;
                            OnLogicUpdate(step, step, stationaryEnemies);
                            setup?.Tick?.Invoke(step);
                        }
                        remainingTicks--;
                        if (!QiziCSList.Any(entity => entity.IsValid) || !DirenList.Any(entity => entity.IsValid)) break;
                        if (timeSliceSimulation && System.Diagnostics.Stopwatch.GetTimestamp() - sliceStart >= sliceBudget) break;
                    }
                    if (timeSliceSimulation && remainingTicks > 0 &&
                        QiziCSList.Any(entity => entity.IsValid) && DirenList.Any(entity => entity.IsValid))
                        yield return null;
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
                        Artifacts = entities[i].ArtifactOutput,
                        CriticalHits = reportCritical[i]?.CriticalHits ?? 0,
                        PearlCharged = reportCritical[i]?.PearlCharged ?? false,
                        MarksApplied = reportCritical[i]?.MarksApplied ?? 0,
                        MarksUsed = reportCritical[i]?.MarksUsed ?? 0,
                        DamageTaken = entities[i].BattleDamageTaken,
                        HealthLost = entities[i].BattleHealthLost,
                        Skills = entities[i].BattleOutput.Values.OrderBy(row => row.SkillId).ToArray()
                    }).ToArray();
                }
                completed(health);
            }
            finally
            {
                IsStationaryTargetPreview = false;
                CaveHazards?.Clear();
                CaveHazards = null;
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
                    foreach (var shadow in QiziCSList.Concat(DirenList).Where(entity => entity.IsPaperShadow))
                        shadow.Remove();
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
                    CriticalRules = false;
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
                    if (mistMaterial != null) Destroy(mistMaterial);
                    if (tidegate) TidegateInkRenderFeature.BattleCamera = previousInkCamera;
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

        private static Mesh BuildTimelineArena(TimelineArenaStyle style, bool saltChannelDrained)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var slate = new Color(.29f, .30f, .28f, .25f);
            var water = new Color(.10f, .19f, .18f, .5f);
            // Vertex alpha selects the surface treatment in TimelineBattleStone.
            var stone = new Color(.21f, .23f, .21f, 0f);
            var wood = new Color(.20f, .13f, .09f, 0f);
            var iron = new Color(.105f, .115f, .107f, 0f);
            var amber = new Color(.87f, .56f, .24f, 0f);

            Color Shade(Color color, float factor) =>
                new Color(color.r * factor, color.g * factor, color.b * factor, color.a);

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            {
                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                for (int i = 0; i < 4; i++) colors.Add(color);
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                for (int i = 0; i < 3; i++) colors.Add(color);
                triangles.AddRange(new[] { start, start + 1, start + 2 });
            }

            void Box(Vector3 center, Vector3 size, Color color, float yaw = 0f)
            {
                Vector3 half = size / 2f;
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 Corner(float x, float z) => center + rotation * new Vector3(x, -half.y, z);
                var a = Corner(-half.x, -half.z);
                var b = Corner(half.x, -half.z);
                var c = Corner(half.x, half.z);
                var d = Corner(-half.x, half.z);
                var up = Vector3.up * size.y;
                Quad(a + up, d + up, c + up, b + up, color);
                Quad(a, b, b + up, a + up, Shade(color, .74f));
                Quad(b, c, c + up, b + up, Shade(color, .85f));
                Quad(c, d, d + up, c + up, Shade(color, .78f));
                Quad(d, a, a + up, d + up, Shade(color, .92f));
            }

            void Cliff(Vector3 foot, Vector3 size, float yaw, Color color)
            {
                const int facets = 6;
                var baseRing = new Vector3[facets];
                var shoulder = new Vector3[facets];
                var crown = new Vector3[facets];
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                for (int i = 0; i < facets; i++)
                {
                    float angle = i * Mathf.PI / 3f;
                    float radius = i % 2 == 0 ? .54f : .46f;
                    var radial = rotation * new Vector3(Mathf.Cos(angle) * size.x * radius, 0f,
                        Mathf.Sin(angle) * size.z * radius);
                    baseRing[i] = foot + radial;
                    shoulder[i] = foot + radial * (i % 3 == 0 ? 1.08f : .86f)
                        + Vector3.up * size.y * (.45f + .08f * (i % 3));
                    crown[i] = foot + radial * .56f + Vector3.up * size.y * (.84f + .07f * (i % 3));
                }
                for (int i = 0; i < facets; i++)
                {
                    int next = (i + 1) % facets;
                    float light = .74f + .10f * ((i + 1) % 4);
                    Quad(baseRing[i], baseRing[next], shoulder[next], shoulder[i], Shade(color, light * .78f));
                    Quad(shoulder[i], shoulder[next], crown[next], crown[i], Shade(color, light));
                    Triangle(foot + Vector3.up * size.y * .96f, crown[next], crown[i],
                        Shade(color, 1.08f + i % 2 * .08f));
                }
            }

            Mesh FinishArena()
            {
                var mesh = new Mesh { name = "Timeline arena " + style };
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                return mesh;
            }

            if (style is TimelineArenaStyle.SaltFlat or TimelineArenaStyle.SaltChannel)
            {
                bool channel = style == TimelineArenaStyle.SaltChannel;
                var salt = new Color(.65f, .61f, .49f, 0f);
                var bank = new Color(.40f, .39f, .31f, 0f);
                var brass = new Color(.47f, .34f, .19f, 0f);
                Box(new Vector3(0, -.88f, 0), new Vector3(34, .45f, 29), Shade(bank, .72f));
                Box(new Vector3(0, -.31f, -.12f), new Vector3(9.6f, .56f, 10.8f), channel ? slate : salt);
                float waterHeight = channel && saltChannelDrained ? -.56f : -.37f;
                Box(new Vector3(-6.1f, waterHeight, 0), new Vector3(2.55f, .06f, 24), water);
                Box(new Vector3(6.1f, waterHeight, 0), new Vector3(2.55f, .06f, 24), water);
                if (channel)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            float z = -5.1f + i * 1.47f;
                            Box(new Vector3(side * 4.93f, -.27f, z), new Vector3(.44f, .7f, 1.44f), Shade(bank, .91f + i % 3 * .05f));
                            Box(new Vector3(side * 5.15f, saltChannelDrained ? -.29f : -.10f, z),
                                new Vector3(.08f, .08f, 1.43f), salt);
                        }
                    }
                    Box(new Vector3(0, .06f, 6.1f), new Vector3(10f, 1.05f, 1.45f), bank);
                    Box(new Vector3(0, .69f, 6.05f), new Vector3(5.6f, .34f, .55f), wood);
                    for (int i = -5; i <= 5; i++)
                        Box(new Vector3(i * .45f, .16f, 5.49f), new Vector3(.31f, .52f, .12f), brass);
                    for (int i = -2; i <= 2; i++)
                        Box(new Vector3(i * 1.34f, -.015f, .04f), new Vector3(.045f, .018f, 8.7f), Shade(bank, .83f));
                }
                else
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float x = -4.7f + i * 1.86f;
                        Box(new Vector3(x, .19f, 5.7f), new Vector3(.13f, .85f, .15f), wood, i % 2 * 9);
                        if (i < 5)
                            Box(new Vector3(x + .9f, .53f, 5.7f), new Vector3(1.86f, .10f, .16f), wood);
                        for (int tag = 0; tag < 3; tag++)
                            Box(new Vector3(x + .27f + tag * .28f, .35f - .03f * (tag % 2), 5.64f),
                                new Vector3(.16f, .27f, .035f), Shade(brass, .83f + tag * .11f), tag * 9f);
                    }
                    for (int i = 0; i < 15; i++)
                    {
                        int side = i % 2 == 0 ? -1 : 1;
                        Box(new Vector3(side * (4.1f + i % 4 * .14f), -.019f, -4.4f + i * .64f),
                            new Vector3(.49f + i % 3 * .14f, .025f, .22f), Shade(salt, 1.08f), i * 29f);
                    }
                }
                return FinishArena();
            }

            if (style is TimelineArenaStyle.IceCave or TimelineArenaStyle.LavaCave)
            {
                bool ice = style == TimelineArenaStyle.IceCave;
                var floor = ice ? new Color(.34f, .50f, .56f, .25f) : new Color(.25f, .225f, .23f, .25f);
                var bank = ice ? new Color(.12f, .21f, .28f, 0) : new Color(.11f, .079f, .085f, 0);
                var river = ice ? new Color(.19f, .40f, .50f, .75f) : new Color(.83f, .20f, .043f, 1f);
                Box(new Vector3(0, -.68f, 5f), new Vector3(60f, .45f, 59f), river);
                if (ice)
                {
                    Box(new Vector3(-.25f, -.20f, 1.2f), new Vector3(10.9f, .34f, 15.2f), floor);
                    var shelf = new Color(.29f, .53f, .60f, 0f);
                    Cliff(new Vector3(-3.9f, -.12f, 8.8f), new Vector3(4.7f, .8f, 3.9f), -14, shelf);
                    Cliff(new Vector3(4.1f, -.14f, 10.5f), new Vector3(5.0f, 1.2f, 4.4f), 39, shelf);
                }
                else
                {
                    var a = new Vector3(-5.1f, -.031f, -5.4f);
                    var b = new Vector3(4.7f, -.031f, -5.4f);
                    var c = new Vector3(5.0f, -.031f, 3.2f);
                    var d = new Vector3(-4.6f, -.031f, 3.2f);
                    var e = new Vector3(3.7f, -.031f, 9.1f);
                    var f = new Vector3(-2.7f, -.031f, 9.1f);
                    Quad(a, d, c, b, floor);
                    Quad(d, f, e, c, floor);
                    var drop = Vector3.down * .43f;
                    var basaltEdge = Shade(floor, .65f);
                    Quad(a, a + drop, d + drop, d, basaltEdge);
                    Quad(d, d + drop, f + drop, f, basaltEdge);
                    Quad(b, c, c + drop, b + drop, basaltEdge);
                    Quad(c, e, e + drop, c + drop, basaltEdge);
                    for (int i = 0; i < 6; i++)
                    {
                        float z = 3.9f + i * 1.48f;
                        Cliff(new Vector3(-5.7f - i * .21f, -.45f, z), new Vector3(.98f, .62f + i * .16f, .97f), i * 15f, bank);
                        Cliff(new Vector3(5.8f + i * .29f, -.45f, z), new Vector3(1.23f, 1.10f + i * .21f, 1.06f), i * 26f, bank);
                    }
                }
                for (int i = 0; i < 9; i++)
                {
                    float z = -3.8f + i * 2.1f;
                    float shift = Mathf.Sin(i * 1.9f) * .9f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Cliff(new Vector3(side * (7.1f + shift), -.45f, z), new Vector3(4.1f, 1.4f + i * .52f, 4.6f),
                            side * i * 27f, bank);
                        if (ice && i >= 2)
                            Cliff(new Vector3(side * (5.35f + Mathf.Abs(shift)), -.04f, z),
                                new Vector3(1.13f, 1.6f + i % 3 * .71f, 1.05f), side * i * 34f,
                                Shade(new Color(.38f, .71f, .78f, 0), .76f + .10f * (i % 3)));
                    }
                }
                for (int i = -3; i <= 3; i++)
                    Cliff(new Vector3(i * 3.7f, -.5f, 16f + Mathf.Abs(i) * 1.3f),
                        new Vector3(5.7f, 3.9f + Mathf.Abs(i) * 1.12f, 5.2f), i * 19f, bank);
                return FinishArena();
            }

            if (style == TimelineArenaStyle.Forest)
            {
                var dirt = new Color(.36f, .30f, .22f, 0f);
                var moss = new Color(.14f, .21f, .09f, 0f);
                var foliage = new Color(.19f, .25f, .14f, 0f);
                Box(new Vector3(0f, -.32f, 2f), new Vector3(48f, .59f, 46f), dirt);

                void Pine(Vector3 root, float scale, int seed)
                {
                    Box(root + Vector3.up * .82f * scale, new Vector3(.24f, 1.66f, .22f) * scale, wood);
                    for (int layer = 0; layer < 3; layer++)
                    {
                        float radius = (.94f - layer * .24f) * scale;
                        var crown = root + Vector3.up * (.66f + layer * .55f) * scale;
                        var tip = crown + Vector3.up * 1.21f * scale;
                        for (int facet = 0; facet < 6; facet++)
                        {
                            float angle = facet * Mathf.PI / 3f + seed * .41f;
                            float next = angle + Mathf.PI / 3f;
                            var left = crown + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                            var right = crown + new Vector3(Mathf.Cos(next), 0f, Mathf.Sin(next)) * radius;
                            Triangle(left, right, tip, Shade(foliage, .71f + .11f * ((facet + layer + seed) % 5)));
                        }
                    }
                }

                for (int side = -1; side <= 1; side += 2)
                {
                    for (int grove = 0; grove < 6; grove++)
                    {
                        float z = -4.2f + grove * 2.12f + side * .40f;
                        float edge = 4.64f + Mathf.Sin(grove * 1.63f + side) * .34f;
                        var root = new Vector3(side * edge, 0f, z);
                        Pine(root, .68f + grove % 3 * .19f, grove);
                        Pine(root + new Vector3(side * 1.22f, 0f, .52f), 1.10f + grove % 2 * .26f, grove + 4);
                        Pine(root + new Vector3(side * 2.39f, 0f, -.62f), .98f + grove % 4 * .15f, grove + 7);
                        Cliff(root + new Vector3(side * .18f, -.07f, -.70f),
                            new Vector3(.83f, .28f + .18f * (grove % 3), .92f), grove * 29f, Shade(stone, 1.23f));
                    }
                    for (int i = 0; i < 54; i++)
                    {
                        float z = -6f + i * .235f;
                        float x = side * (3.86f + .98f * Mathf.PerlinNoise(i * .57f, side + 2f));
                        float scale = .78f + Mathf.PerlinNoise(i * 1.53f, 7f) * .85f;
                        var root = new Vector3(x, -.018f, z);
                        for (int blade = 0; blade < 4; blade++)
                        {
                            float angle = i * 2.39f + blade * 1.63f;
                            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                            var across = Vector3.Cross(Vector3.up, direction) * (.063f * scale);
                            var start = root + direction * (.08f * blade);
                            var tip = start + direction * (.19f * scale) + Vector3.up * (.19f + blade * .052f) * scale;
                            Triangle(start - across, start + across, tip, Shade(moss, .83f + blade * .14f));
                        }
                    }
                }
                for (int i = 0; i < 9; i++)
                {
                    float z = 3.78f + i * .66f;
                    float x = .3f + Mathf.Sin(i * .62f) * .92f;
                    Box(new Vector3(x, -.045f, z), new Vector3(.97f - i % 3 * .18f, .07f, .46f),
                        Shade(stone, 1.27f + i % 2 * .09f), 13f - i * 3f);
                }
                return FinishArena();
            }

            if (style == TimelineArenaStyle.Cliff)
            {
                var ledge = new Color(.31f, .28f, .25f, 0f);
                var escarpment = new Color(.25f, .23f, .22f, 0f);
                var basin = new Color(.17f, .20f, .21f, 0f);
                Box(new Vector3(0f, -4.1f, 0f), new Vector3(180f, .6f, 180f), basin);
                Vector3 Edge(int section)
                {
                    float z = -11f + section * 1.75f;
                    return new Vector3(4.07f + .65f * Mathf.PerlinNoise(section * .75f, .5f), -.025f, z);
                }
                for (int i = 0; i < 15; i++)
                {
                    var front = Edge(i);
                    var back = Edge(i + 1);
                    var left = new Vector3(-9f, front.y, front.z);
                    var leftBack = new Vector3(-9f, back.y, back.z);
                    var frontSlope = front + new Vector3(.73f + i % 2 * .44f, -1.25f, 0f);
                    var backSlope = back + new Vector3(.73f + (i + 1) % 2 * .44f, -1.25f, 0f);
                    var frontBase = frontSlope + new Vector3(.81f, -2.7f, 0f);
                    var backBase = backSlope + new Vector3(.81f, -2.7f, 0f);
                    Quad(left, leftBack, back, front, Shade(ledge, .95f + .023f * (i % 4)));
                    Quad(front, back, backSlope, frontSlope, Shade(escarpment, .83f + .09f * (i % 3)));
                    Quad(frontSlope, backSlope, backBase, frontBase, Shade(escarpment, .63f + .08f * (i % 4)));
                }
                for (int i = 0; i < 10; i++)
                {
                    float z = -6.5f + i * 2.08f;
                    float height = 2.9f + .45f * (i % 4);
                    Cliff(new Vector3(-5.9f - .25f * (i % 3), -.25f, z),
                        new Vector3(3.9f, height, 4.1f), i * 34f, Shade(escarpment, .79f + .08f * (i % 4)));
                    Cliff(new Vector3(9.2f + (i % 3) * 1.32f, -4.0f, z + 1.7f),
                        new Vector3(2.55f, 1.2f + .51f * (i % 4), 2.86f), i * 52f, basin);
                }
                for (int i = 0; i < 6; i++)
                {
                    var edge = Edge(i + 4);
                    var post = edge + new Vector3(-.24f, .22f, .25f);
                    Box(post, new Vector3(.19f, .48f, .23f), wood, i * 9f);
                    if (i < 5 && i != 2)
                    {
                        var next = Edge(i + 5) + new Vector3(-.24f, .22f, .25f);
                        var direction = next - post;
                        Box((post + next) * .5f, new Vector3(.045f, .042f, direction.magnitude),
                            Shade(wood, 1.37f), Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
                    }
                }
                return FinishArena();
            }

            if (style == TimelineArenaStyle.Ritual)
            {
                var courtyard = new Color(.19f, .17f, .16f, 0f);
                var platform = new Color(.26f, .23f, .21f, 0f);
                var inlay = new Color(.46f, .24f, .18f, 0f);
                Box(new Vector3(0f, -.60f, 0f), new Vector3(47f, .60f, 47f), courtyard);

                Vector3 Polar(float angle, float radius, float height) =>
                    new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);

                const int segments = 48;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * 2f * Mathf.PI / segments;
                    float next = (i + 1) * 2f * Mathf.PI / segments;
                    var near = Polar(angle, 5.22f, -.025f);
                    var far = Polar(next, 5.22f, -.025f);
                    Triangle(new Vector3(0f, -.025f, 0f), far, near, Shade(platform, .96f + .012f * (i % 4)));
                    Quad(near, far, Polar(next, 5.51f, -.15f), Polar(angle, 5.51f, -.15f), Shade(platform, .73f));
                    Quad(Polar(angle, 5.51f, -.15f), Polar(next, 5.51f, -.15f),
                        Polar(next, 5.77f, -.295f), Polar(angle, 5.77f, -.295f), Shade(platform, .60f));
                    if (i % 8 == 0 || i % 11 == 0) continue;
                    float end = next - .009f;
                    Quad(Polar(angle, 4.59f, -.016f), Polar(end, 4.59f, -.016f),
                        Polar(end, 4.73f, -.016f), Polar(angle, 4.73f, -.016f), Shade(inlay, .86f + .064f * (i % 4)));
                    if (i % 4 == 1)
                        Quad(Polar(angle, 4.10f, -.015f), Polar(angle + .032f, 4.10f, -.015f),
                            Polar(angle + .032f, 4.49f, -.015f), Polar(angle, 4.49f, -.015f), inlay);
                }

                for (int i = 0; i < 7; i++)
                {
                    float angle = (.02f + i * .16f) * Mathf.PI;
                    var foot = Polar(angle, 6.17f, -.28f);
                    float height = i % 3 == 1 ? 1.9f : 1.26f;
                    float yaw = 90f - angle * Mathf.Rad2Deg;
                    Box(foot + Vector3.up * .13f, new Vector3(.87f, .31f, .87f), platform, yaw);
                    Box(foot + Vector3.up * (.27f + height * .5f), new Vector3(.48f, height, .49f),
                        Shade(platform, .80f), yaw + i % 2 * 7f);
                    Cliff(foot + Vector3.up * (height + .20f), new Vector3(.68f, .24f, .65f), yaw, platform);
                }
                for (int step = 0; step < 4; step++)
                    Box(new Vector3(0f, -.20f + step * .14f, 5.60f + step * .28f),
                        new Vector3(3.84f - step * .23f, .19f, 2.42f - step * .32f), Shade(platform, .83f + step * .10f));
                for (int side = -1; side <= 1; side += 2)
                {
                    var shard = new Vector3(side * .92f, .31f, 6.55f);
                    Cliff(shard, new Vector3(1.48f, 1.24f + side * .18f, 1.10f), side * 23f, Shade(courtyard, .84f));
                    Box(new Vector3(side * 2.08f, -.05f, 5.7f), new Vector3(.46f, .52f, .46f), iron);
                    Box(new Vector3(side * 2.08f, .24f, 5.7f), new Vector3(.35f, .09f, .35f), Shade(amber, .73f));
                }
                return FinishArena();
            }

            // The paving extends toward the camera and joins both abutments.
            slate = new Color(.43f, .40f, .34f, .25f);
            water = new Color(.136f, .19f, .179f, .5f);
            stone = new Color(.24f, .267f, .24f, 0f);
            wood = new Color(.205f, .14f, .097f, 0f);
            Box(new Vector3(-.25f, -.53f, -3.4f), new Vector3(9.4f, 1.01f, 16.6f), slate);
            Quad(new Vector3(-32, -.59f, -32), new Vector3(-32, -.59f, 32),
                new Vector3(32, -.59f, 32), new Vector3(32, -.59f, -32), water);

            // Sluice posts, lintels and vertical boards sit behind the far row.
            const float gateZ = 4.75f;
            // The water plane continues below the dam. Solid masonry closes the openings
            // behind the door planks, and the rear embankment covers the water above them.
            Box(new Vector3(-.25f, .45f, gateZ + .83f), new Vector3(9.2f, 2.55f, 1.30f), Shade(stone, .69f));
            Box(new Vector3(-.25f, .38f, gateZ + 6.8f), new Vector3(19f, 2.6f, 11.3f), Shade(stone, .81f));
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * 4.3f;
                Cliff(new Vector3(x, -.70f, 17f + i % 3 * 2.9f),
                    new Vector3(6.2f, 3.2f + (i * 7 % 5) * .83f, 6.4f),
                    i * 23f, Shade(stone, .92f));
            }
            Box(new Vector3(-.25f, 1.85f, gateZ + .65f), new Vector3(9.48f, .33f, 1.76f), Shade(stone, 1.12f));
            Box(new Vector3(-.25f, 2.04f, gateZ + .72f), new Vector3(9.2f, .13f, 1.58f), stone);
            for (int i = -6; i <= 6; i++)
            {
                Box(new Vector3(-.25f + i * .71f, 2.23f, gateZ + .08f),
                    new Vector3(.44f, .30f, .37f), Shade(stone, 1.02f + .04f * (i % 3)));
            }
            for (int i = -1; i <= 1; i++)
            {
                float x = -.25f + i * 2.7f;
                Box(new Vector3(x, .02f, gateZ), new Vector3(1.06f, .42f, 1.34f), Shade(stone, .85f));
                Box(new Vector3(x, .60f, gateZ), new Vector3(.70f, 2.55f, .94f), stone);
                Box(new Vector3(x, 1.68f, gateZ), new Vector3(.80f, .14f, 1.02f), Shade(stone, .83f));
                Box(new Vector3(x, 1.88f, gateZ), new Vector3(.94f, .28f, 1.10f), stone);
            }
            for (int i = 0; i < 14; i++)
            {
                float x = -2.62f + i * .365f;
                Box(new Vector3(x, .67f, gateZ + .14f), new Vector3(.335f, 1.48f, .19f),
                    Shade(wood, .86f + .08f * (i % 4)));
            }
            Box(new Vector3(-.25f, 1.56f, gateZ - .09f), new Vector3(6.05f, .30f, .67f), wood);
            Box(new Vector3(-.25f, .93f, gateZ - .10f), new Vector3(5.12f, .16f, .18f), wood);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = -.25f + side * 2.7f;
                float bay = -.25f + side * 1.35f;
                Box(new Vector3(bay, -.11f, gateZ - .05f), new Vector3(2.08f, .19f, .78f), Shade(stone, .87f));
                Box(new Vector3(bay, 1.08f, gateZ - .25f), new Vector3(.18f, 1.18f, .10f), iron);
                Box(new Vector3(bay, 1.33f, gateZ - .32f), new Vector3(.39f, .13f, .14f), iron);
                float lanternZ = gateZ - .61f;
                Box(new Vector3(x, 1.42f, lanternZ), new Vector3(.19f, .28f, .20f), amber);
                Box(new Vector3(x, 1.59f, lanternZ), new Vector3(.27f, .065f, .28f), iron);
                Box(new Vector3(x, 1.25f, lanternZ), new Vector3(.24f, .055f, .25f), iron);
                Box(new Vector3(x, 1.42f, lanternZ - .104f), new Vector3(.026f, .29f, .03f), iron);
                Box(new Vector3(-.25f + side * 4.0f, .23f, gateZ), new Vector3(2.6f, 1.65f, 2.5f), stone);
                for (int tier = 0; tier < 3; tier++)
                {
                    float inset = tier * .21f;
                    Box(new Vector3(-.25f + side * 4.28f, 1.14f + tier * .35f, gateZ + .66f + inset),
                        new Vector3(2.24f - inset, .34f, 2.34f - inset),
                        Shade(stone, .86f + tier * .08f));
                }
                Cliff(new Vector3(-.25f + side * 6.6f, -.83f, 5.7f),
                    new Vector3(4.6f, 3.7f, 4.9f), side * 22f, Shade(stone, .83f));

                // Low coping defines the edge of the fightable terrace without covering the nearest row.
                for (int i = 0; i < 6; i++)
                {
                    if ((i + side + 1) % 5 == 1) continue;
                    float z = -4.5f + i * 1.53f;
                    Box(new Vector3(-.25f + side * 4.40f, .08f, z),
                        new Vector3(.42f, .23f, 1.46f), Shade(stone, 1.10f + .05f * (i % 3)));
                }

                for (int i = 0; i < 7; i++)
                {
                    float z = -5.5f + i * 1.9f;
                    float height = .90f + (i % 3) * .36f + (i > 3 ? .40f : 0f);
                    Cliff(new Vector3(-.25f + side * (5.95f + (i % 2) * .31f), -.82f, z),
                        new Vector3(3.6f, height, 3.3f), side * (17f + i * 28f), Shade(stone, .88f + .065f * (i % 4)));
                }
            }
            return FinishArena();
        }
    }
}
