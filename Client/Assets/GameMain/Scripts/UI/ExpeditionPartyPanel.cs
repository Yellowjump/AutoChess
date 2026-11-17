using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;
using WorldTimelineDebug;

// Shared by the map's roster button and the mandatory pre-battle formation step.
public sealed class ExpeditionPartyPanel : MonoBehaviour
{
    private WorldTimelineDebugView view;
    private TMP_FontAsset font;
    private bool preBattle;
    private bool restoreDebugger;
    private int battleNode, selected;
    private Action closed;
    private Action<int> showLocation;
    private readonly Button[] roster = new Button[5], actions = new Button[4], slots = new Button[6];
    private TMP_Text details, instruction;
    private string feedback;
    private ExpeditionParty Party => view.Expedition;
    private static readonly Color Ink = new Color(.10f, .16f, .17f), Paper = new Color(.92f, .89f, .81f),
        Jade = new Color(.22f, .43f, .38f), Gold = new Color(.66f, .49f, .25f);

    public static ExpeditionPartyPanel Open(WorldTimelineDebugView view, TMP_FontAsset font, bool preBattle,
        int nodeId, Action closed, Action<int> showLocation = null)
    {
        var root = new GameObject("队伍与布阵", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var panel = root.AddComponent<ExpeditionPartyPanel>();
        panel.view = view; panel.font = font; panel.preBattle = preBattle;
        panel.battleNode = nodeId; panel.closed = closed; panel.showLocation = showLocation;
        panel.selected = view.Simulation.State.PlayerActorId;
        panel.restoreDebugger = HotGameEntry.Debugger != null && HotGameEntry.Debugger.ActiveWindow;
        if (panel.restoreDebugger) HotGameEntry.Debugger.ActiveWindow = false;
        view.ExpeditionPanelOpen = true;
        panel.Build(); panel.Refresh();
        // Populate the shared dynamic atlas before the first canvas render, then rebuild every label.
        // Adding glyphs label by label can otherwise leave earlier meshes using an outdated atlas.
        var labels = root.GetComponentsInChildren<TMP_Text>();
        if (font.atlasPopulationMode != AtlasPopulationMode.Static)
            font.TryAddCharacters(string.Concat(labels.Select(label => label.text)));
        Canvas.ForceUpdateCanvases();
        foreach (var label in labels) label.ForceMeshUpdate();
        return panel;
    }

    private void Build()
    {
        Box(transform, "Background", 0, 0, 1920, 1080, Ink);
        Label(transform, "Title", preBattle ? "关前布阵" : "同行队伍", 55, 40, 1300, 70, 46);
        Label(transform, "Subtitle", "查看同伴与招募地点 · 到达战斗关卡后，在真实战场调整上阵与站位", 55, 118, 1750, 50, 26);
        Label(transform, "RosterTitle", "队伍与招募地点", 55, 200, 690, 45, 28);
        for (int i = 0; i < 5; i++)
        {
            int id = i == 0 ? view.Simulation.State.PlayerActorId : ExpeditionParty.FirstCompanionId + i - 1;
            roster[i] = Button(transform, "Member" + i, "", 55, 260 + i * 127, 470, 110,
                () => { selected = id; feedback = null; Refresh(); });
            if (i == 0) continue;
            int role = i - 1;
            actions[role] = Button(transform, "Recruit" + role, "", 540, 260 + i * 127, 200, 110,
                () => ActOnRole(role));
        }
        Label(transform, "FormationTitle", "同伴详情", 790, 200, 1050, 45, 28);
        for (int i = 0; preBattle && i < slots.Length; i++)
        {
            int cell = i;
            slots[i] = Button(transform, "FormationCell" + i, "", 790 + i % 3 * 350, 270 + i / 3 * 155, 330, 135,
                () =>
                {
                    feedback = view.PlaceCompanion(selected, cell) ? "阵型已更新。" :
                        !Party.Owns(selected) ? "先到队友所在地点，点击「招募入队」。" :
                        "最多三人上阵。请替换队友所在格子，或先将队友下阵。主角必须上阵。";
                    Refresh();
                });
        }
        details = Label(transform, "Details", "", 795, preBattle ? 600 : 280, 1030, preBattle ? 285 : 580, 26);
        details.enableAutoSizing = true; details.fontSizeMin = 22; details.fontSizeMax = 26;
        instruction = Label(transform, "Instructions", "", 55, 905, 1250, 120, 25);
        Button(transform, "Confirm", preBattle ? "按此阵型开战" : "返回地图", 1360, 940, 470, 70, Close, Gold);
    }

    private string Name(int id) => id == 0 ? "空位" : id == view.Simulation.State.PlayerActorId
        ? view.PlayerName + " · 主角" : view.CompanionName(ExpeditionParty.Role(id));

    private void ActOnRole(int role)
    {
        int id = ExpeditionParty.FirstCompanionId + role;
        if (Party.HasRecruited(role))
        {
            selected = id;
            if (!preBattle) { feedback = "已招募。进入战斗关卡后，可在战场上调整上阵与站位。"; Refresh(); return; }
            feedback = Party.CellOf(id) >= 0
                ? (view.BenchCompanion(id) ? "已下阵，仍保留在队伍候补中。" : "暂时无法下阵。")
                : "已选中候补。点击空位上阵，或点击队友所在格子替换。";
            Refresh(); return;
        }
        if (!preBattle && showLocation != null)
        {
            var focus = showLocation;
            Close(); focus(role);
        }
    }

    private void Refresh()
    {
        var world = view.Simulation.State;
        var player = world.Actors[world.PlayerActorId];
        for (int i = 0; i < roster.Length; i++)
        {
            int role = i - 1, id = i == 0 ? player.Id : ExpeditionParty.FirstCompanionId + role;
            bool owned = Party.Owns(id);
            string state = !owned ? "未招募" : Party.CellOf(id) >= 0 ? "上阵" : "候补";
            var actor = i == 0 ? player : Party.CreateCompanion(role, player.NodeId ?? player.BirthNodeId);
            string location = "";
            if (!owned && Party.RecruitmentSites.TryGetValue(role, out int nodeId))
            {
                var node = world.Nodes[nodeId];
                location = "\n" + view.LocationName(node) + $" ({node.Q}, {node.R})";
            }
            roster[i].GetComponentInChildren<TMP_Text>().text = Name(id) + "  [" + state + "]\n" +
                $"气血 {actor.MaxHealth} · 攻击 {actor.CombatAttack}" + location;
            roster[i].targetGraphic.color = selected == id ? Gold : Jade;
            if (i == 0) continue;
            actions[role].GetComponentInChildren<TMP_Text>().text = owned ? (!preBattle ? "查看同伴" : Party.CellOf(id) >= 0 ? "下阵候补" : "选择上阵")
                : preBattle ? "尚未招募" : "查看同伴关卡";
            actions[role].interactable = owned || !preBattle && Party.RecruitmentSites.ContainsKey(role);
        }
        for (int cell = 0; cell < slots.Length; cell++)
        {
            if (slots[cell] == null) continue;
            slots[cell].GetComponentInChildren<TMP_Text>().text = (cell < 3 ? "前排" : "后排") + "\n" + Name(Party.Cells[cell]);
            slots[cell].targetGraphic.color = Party.Cells[cell] == selected ? Gold : Jade;
        }
        string ability = ExpeditionParty.Role(selected) switch
        {
            0 => "护盾与牵制：定期获得护盾，吸引近身敌人攻击自己。放在前排接敌，才能及时掩护输出。",
            1 => "近战突破：高伤害近战，攻击附带流血。让盾卫接敌后集中突破一侧。",
            2 => "远程输出：射程6格，气血较低。放在后排，保持输出空间。",
            3 => "治疗支援：每3秒治疗4.5格内最虚弱的友军，恢复主角最大气血的18%。",
            _ => "主角保留当前装备、功法与气血。站位会影响接敌顺序，也决定队友能否及时保护你。"
        };
        string enemy = "";
        if (preBattle && world.Nodes.TryGetValue(battleNode, out var battle))
        {
            enemy = view.LocationName(battle) + (battle.MonsterHealth > 0
                ? $" · 守军 {battle.GuardianCount} 人 · 总气血 {battle.MonsterHealth} · 攻击 {battle.MonsterAttack}"
                : " · 遭遇来客") + "\n\n";
            if (Party.ProgressionEnabled)
                enemy += $"胜利经验 +{Party.VictoryExperience(battleNode, view.Simulation.Cultivation)} · 战后恢复25%最大气血\n\n";
            if (battle.IsExpeditionVanguard && battle.MonsterHealth > 0)
                enemy += "<b>敌阵：两名近战守军，正面逼近</b>\n先攻击接近的目标，无远程火力。\n\n";
        }
        details.text = enemy + "<b>" + Name(selected) + "</b>\n" + ability;
        instruction.text = (feedback ?? $"已上阵 {Party.DeployedCount} / 3 人。前往地图上的同伴关卡，满足条件后招募。") +
            "\n探索与通关提升全队实力；队友每战满血，主角胜利后恢复25%最大气血。";
        if (preBattle && world.Nodes[battleNode].IsExpeditionVanguard && world.Nodes[battleNode].MonsterHealth > 0)
            instruction.text = view.VanguardFormationHint + "\n可自由调整阵型；战后查看承伤与护盾吸收，检验这次布阵。";
    }

    private void Update()
    {
        if (view == null) { Close(); return; }
        if (!preBattle && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void Close()
    {
        if (view != null) view.ExpeditionPanelOpen = false;
        var callback = closed; closed = null;
        gameObject.SetActive(false); Destroy(gameObject); callback?.Invoke();
    }

    private void OnDestroy()
    {
        if (view != null) view.ExpeditionPanelOpen = false;
        if (restoreDebugger && HotGameEntry.Debugger != null) HotGameEntry.Debugger.ActiveWindow = true;
    }

    private RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
    }
    private RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
    { var rect = Rect(parent, name, x, y, w, h); rect.gameObject.AddComponent<Image>().color = color; return rect; }
    private TMP_Text Label(Transform parent, string name, string text, float x, float y, float w, float h, float size)
    {
        var label = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = size; label.color = Paper;
        label.raycastTarget = false; label.enableWordWrapping = true; return label;
    }
    private Button Button(Transform parent, string name, string text, float x, float y, float w, float h, Action click, Color? color = null)
    {
        var rect = Box(parent, name, x, y, w, h, color ?? Jade);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var label = Label(rect, "Label", text, 10, 6, w - 20, h - 12, 25); label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => click()); UIFormLogic.BindButtonClickSound(button); return button;
    }
}
