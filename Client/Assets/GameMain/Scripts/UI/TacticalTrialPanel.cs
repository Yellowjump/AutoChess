using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameFramework.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimeline;

// Self-contained UGUI encounter screen. The trial never opens or writes a campaign save.
public sealed class TacticalTrialPanel : MonoBehaviour
{
    public TacticalTrial Formation { get; } = new TacticalTrial();
    public TacticalTrialResult LastResult { get; private set; }
    public bool IsFighting => routines.Count > 0;
    private readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private readonly Button[] cards = new Button[4], slots = new Button[6];
    private TMP_FontAsset font;
    private GameObject planning, watching;
    private TMP_Text instructions, report, live, count, selectedLabel;
    private Button fight;
    private Action closed;
    private int selected = 0;
    private float elapsed;
    private bool english;
    private bool restoreDebugger;
    private string error;
    private TacticalTrialResult previous;
    private static readonly Color Ink = new Color(.10f, .16f, .17f);
    private static readonly Color Paper = new Color(.92f, .89f, .81f);
    private static readonly Color Gold = new Color(.76f, .60f, .34f);
    private static readonly Color Jade = new Color(.22f, .43f, .38f);
    private static readonly Color Muted = new Color(.63f, .69f, .67f);

    public static TacticalTrialPanel Open(TMP_FontAsset font, Action closed)
    {
        var root = new GameObject("山门试阵", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var screen = root.AddComponent<TacticalTrialPanel>();
        screen.font = font;
        screen.closed = closed;
        screen.english = GameEntry.Localization.Language == Language.English;
        screen.restoreDebugger = HotGameEntry.Debugger != null && HotGameEntry.Debugger.ActiveWindow;
        if (screen.restoreDebugger) HotGameEntry.Debugger.ActiveWindow = false;
        screen.Build();
        screen.Refresh();
        EventSystem.current?.SetSelectedGameObject(null);
        return screen;
    }

    private string T(string cn, string en) => english ? en : cn;
    public string RoleName(int role) => role switch
    {
        0 => T("重山 · 盾修", "Chongshan · Defender"),
        1 => T("赤砾 · 剑修", "Chili · Swordsman"),
        2 => T("闻潮 · 远攻", "Wenchao · Marksman"),
        3 => T("白溪 · 医者", "Baixi · Healer"),
        _ => T("空位", "Empty")
    };

    private string RoleDescription(int role) => role switch
    {
        0 => T("定期获得护盾，牵制3格内的敌人。\n让敌人优先攻击自己，掩护队友。", "Periodic shield. Draws enemies within 3 cells.\nEnemies target him instead of nearby allies."),
        1 => T("近战高输出，攻击附加流血。\n需要前排掩护，集中突破一侧。", "Strong melee attacks inflict bleeding.\nProtect him and overwhelm one flank."),
        2 => T("远程持续输出。\n气血较低，适合留在后排。", "Sustained ranged damage.\nLow health: keep him behind your defender."),
        _ => T("每3秒为4.5格内最虚弱的友军\n恢复115气血，自身输出较低。", "Every 3s, heal the most injured ally\nwithin 4.5 cells for 115 HP. Low damage.")
    };

    private void Build()
    {
        planning = Box(transform, "Planning", 0, 0, 1920, 1080, Ink).gameObject;
        Label(planning.transform, "Title", T("山门试阵", "THE MOUNTAIN GATE"), 55, 42, 1200, 65, 48, Paper);
        Label(planning.transform, "Subtitle", T("三人破阵 · 45秒内击溃守军 · 换人、换位，找到过关的方法", "Three cultivators. Break the guard within 45 seconds. Change your team and formation."),
            58, 113, 1500, 48, 25, Muted);
        MakeButton(planning.transform, "Close", T("返回主菜单", "Back to menu"), 1620, 60, 240, 58, Close, Jade);
        Box(planning.transform, "Divider", 55, 175, 1810, 2, Gold);
        Label(planning.transform, "RosterTitle", T("备战修士 · 四选三", "ROSTER · PICK THREE"), 58, 202, 470, 42, 27, Gold);
        for (int i = 0; i < cards.Length; i++)
        {
            int role = i;
            cards[i] = MakeButton(planning.transform, "Role" + i, "", 55, 265 + i * 145, 465, 130,
                () => SelectRole(role), Jade, 23);
        }
        selectedLabel = Label(planning.transform, "Selected", "", 58, 858, 465, 55, 23, Gold);
        MakeButton(planning.transform, "Bench", T("选中角色下阵", "Bench selected"), 55, 940, 465, 64,
            () => { Formation.Bench(selected); Refresh(); }, new Color(.24f, .29f, .28f));

        Label(planning.transform, "EnemyTitle", T("敌方阵型 · 上方为敌方后排", "ENEMY · BACK ROW AT TOP"), 565, 202, 745, 42, 27, Gold);
        for (int i = 0; i < 3; i++)
        {
            float x = 565 + i * 249;
            Box(planning.transform, "EnemyBack" + i, x, 265, 232, 95, new Color(.18f, .22f, .22f));
            Label(planning.transform, "EnemyBackLabel" + i, i == 2 ? T("医者\n420 气血", "Healer\n420 HP") : "—", x, 265, 232, 95, 24, Paper, true);
            Box(planning.transform, "EnemyFront" + i, x, 378, 232, 95, new Color(.30f, .23f, .22f));
            Label(planning.transform, "EnemyFrontLabel" + i, i != 1 ? T("盾卫\n740 气血", "Shield Guard\n740 HP") : "—", x, 378, 232, 95, 24, Paper, true);
        }
        Label(planning.transform, "FriendlyTitle", T("我方阵型 · 前排靠近敌人", "YOUR FORMATION · FRONT ROW FIRST"), 565, 505, 745, 40, 27, Gold);
        for (int i = 0; i < slots.Length; i++)
        {
            int cell = i;
            slots[i] = MakeButton(planning.transform, "Cell" + i, "", 565 + i % 3 * 249,
                565 + i / 3 * 140, 232, 120, () => PlaceSelected(cell), Jade, 25);
        }
        instructions = Label(planning.transform, "Instructions", "", 565, 853, 745, 70, 24, Paper);
        count = Label(planning.transform, "Count", "", 565, 950, 745, 60, 24, Gold);

        Box(planning.transform, "ReportBackground", 1360, 205, 505, 690, new Color(.15f, .21f, .22f));
        report = Label(planning.transform, "Report", "", 1390, 230, 445, 635, 25, Paper);
        fight = MakeButton(planning.transform, "Fight", T("开始战斗", "Begin battle"), 1360, 940, 505, 64, StartFight, Gold, 29);
        Label(planning.transform, "FixedStats", T("固定属性 · 每战恢复满血 · 无修炼", "Fixed stats · Full health each attempt"), 1360, 1018, 505, 32, 20, Muted, true);

        watching = Box(transform, "Watching", 0, 0, 1920, 115, new Color(.07f, .12f, .13f, .95f)).gameObject;
        live = Label(watching.transform, "Live", "", 55, 20, 1450, 78, 25, Paper);
        MakeButton(watching.transform, "Retreat", T("撤回布阵", "Return to formation"), 1600, 30, 265, 58, CancelFight, Jade);
        watching.SetActive(false);
    }

    public void SelectRole(int role)
    {
        if (IsFighting || role < 0 || role > 3) return;
        selected = role;
        error = null;
        Refresh();
    }

    public void PlaceSelected(int cell)
    {
        if (IsFighting) return;
        error = Formation.Place(selected, cell) ? null : T("最多三人上阵。点击已有角色的格子替换，或先下阵。",
            "Three slots are available. Replace an occupied cell or bench a unit first.");
        Refresh();
    }

    public void StartFight()
    {
        if (IsFighting || !Formation.CanFight) return;
        previous = LastResult;
        LastResult = null;
        elapsed = 0;
        error = null;
        routines.Push(TacticalTrialBattle.Run(Formation.BeginAttempt(), true, result => LastResult = result,
            seconds => elapsed = seconds));
        planning.SetActive(false);
        watching.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsFighting) CancelFight(); else Close();
            return;
        }
        if (!IsFighting) return;
        live.text = T($"山门交战 · 第 {Formation.Attempts} 次 · 剩余 {Mathf.Max(0, TacticalTrialBattle.TimeLimit - elapsed):0} 秒\n观察前排承伤、集火方向与医者的治疗。",
            $"Gate battle · Attempt {Formation.Attempts} · {Mathf.Max(0, TacticalTrialBattle.TimeLimit - elapsed):0}s left\nWatch who takes damage, your focus fire and the healer.");
        try
        {
            // Own the entire iterator stack so retreat also executes the battle's finally cleanup.
            for (int budget = 0; budget < 64 && routines.Count > 0; budget++)
            {
                var current = routines.Peek();
                if (!current.MoveNext())
                { routines.Pop(); (current as IDisposable)?.Dispose(); continue; }
                if (current.Current is IEnumerator nested) { routines.Push(nested); continue; }
                break;
            }
            if (routines.Count == 0) ShowPlanning();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            DisposeBattle();
            error = T("战斗未能完成，已返回布阵。", "Battle could not finish. Returned to formation.");
            ShowPlanning();
        }
    }

    public void CancelFight()
    {
        if (!IsFighting) return;
        DisposeBattle();
        LastResult = previous;
        error = T("已撤回布阵。再次挑战时双方恢复满血。", "Returned to formation. Both sides start at full health next time.");
        ShowPlanning();
    }

    private void DisposeBattle()
    {
        while (routines.Count > 0)
        {
            try { (routines.Pop() as IDisposable)?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    private void ShowPlanning()
    {
        planning.SetActive(true);
        watching.SetActive(false);
        Refresh();
    }

    private void Refresh()
    {
        for (int role = 0; role < cards.Length; role++)
        {
            var text = cards[role].GetComponentInChildren<TMP_Text>();
            text.text = $"<b>{RoleName(role)}</b>  {(Formation.CellOf(role) >= 0 ? T("[上阵]", "[IN]") : T("[候补]", "[BENCH]"))}\n" +
                T($"气血 {TrialDeployment.Health(role)} / 攻击 {TrialDeployment.Attack(role)}", $"HP {TrialDeployment.Health(role)} / ATK {TrialDeployment.Attack(role)}") + "\n" + RoleDescription(role);
            cards[role].targetGraphic.color = role == selected ? new Color(.35f, .39f, .28f) : new Color(.19f, .28f, .27f);
        }
        for (int cell = 0; cell < slots.Length; cell++)
        {
            int role = Formation.Cells[cell];
            slots[cell].GetComponentInChildren<TMP_Text>().text = (cell < 3 ? T("前排", "FRONT") : T("后排", "BACK")) +
                "\n" + RoleName(role);
            slots[cell].targetGraphic.color = role == selected ? new Color(.40f, .43f, .29f) : role < 0 ? new Color(.16f, .23f, .23f) : Jade;
        }
        selectedLabel.text = T("当前选中：", "Selected: ") + RoleName(selected);
        instructions.text = error ?? T("先选左侧角色，再点格子部署。\n点击已占用格子可换位或替换角色。", "Select a cultivator, then a cell.\nOccupied cells swap or replace units.");
        count.text = T($"已上阵 {Formation.DeployedCount} / 3 人 · 敌方阵容保持不变", $"Deployed {Formation.DeployedCount} / 3 · Enemy formation stays fixed");
        fight.interactable = Formation.CanFight;
        fight.GetComponentInChildren<TMP_Text>().text = LastResult == null ? T("开始战斗", "Begin battle") : T("按当前阵型再战", "Fight with this formation");
        report.text = LastResult == null ? T(
            "<b>山门守军</b>\n\n盾卫各有一次开场护盾。右后方医者每2.5秒为4.5格内的盾卫恢复90气血，无法自疗。\n\n<b>如何破阵？</b>\n\n集中攻击一侧，避免输出分散。\n让盾修先接敌，保护剑修和远攻。\n可换医者增加续航，但会少一名输出。\n\n45秒内击溃全部守军。敌方不会随重试变强。",
            "<b>THE GATE GUARD</b>\n\nEach guard starts with one shield. Every 2.5s, the healer restores 90 HP to a guard within 4.5 cells. He cannot heal himself.\n\n<b>PLAN YOUR APPROACH</b>\n\nFocus one flank.\nLet your defender engage first.\nA healer improves endurance, at the cost of a damage dealer.\n\nDefeat all enemies within 45s. Enemies do not scale with retries.") : ResultText(LastResult);
    }

    private string ResultText(TacticalTrialResult result)
    {
        string title = result.Victory ? T("山门已破", "GATE BROKEN") : result.TimedOut ? T("时限已到 · 尚未破阵", "TIME UP · GATE HOLDS") : T("尚未破阵", "GATE HOLDS");
        var lines = new List<string> { $"<b>{title}</b> · {result.Seconds:0.0}s", "", T("角色输出 / 剩余气血", "DAMAGE / REMAINING HP") };
        for (int i = 0; i < result.Roles.Length; i++)
            lines.Add($"{RoleName(result.Roles[i])}\n{result.Damage[i]} / {result.Health[i]}");
        lines.Add(T($"\n治疗：我方 {result.AlliedHealing} / 敌方 {result.EnemyHealing}", $"\nHealing: allies {result.AlliedHealing} / enemy {result.EnemyHealing}"));
        if (result.FirstFallenId != 0)
        {
            string first = result.FirstFallenId < 10 ? RoleName(result.FirstFallenId - 1) :
                result.FirstFallenId == 13 ? T("敌方医者", "Enemy healer") : T("敌方盾卫", "Enemy guard");
            lines.Add(T($"{result.FirstFallenAt:0.0}秒，{first}最先倒下。", $"First fallen at {result.FirstFallenAt:0.0}s: {first}."));
        }
        lines.Add(result.Victory ? T("\n试试保住更多队友，或换另一种组合破阵。", "\nTry keeping more allies alive, or win with another team.") :
            result.FirstFallenId is 2 or 3 or 4 ? T("\n输出或医者先倒下了。尝试让盾修站在接敌的位置。", "\nA fragile ally fell first. Put your defender in the path of the guards.") :
            T("\n尝试集中一侧输出，或用医者换取更长的作战时间。", "\nTry focusing one flank, or trade damage for a healer's endurance."));
        return string.Join("\n", lines);
    }

    public void Close()
    {
        DisposeBattle();
        var callback = closed;
        closed = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
        callback?.Invoke();
    }

    private void OnDestroy()
    {
        DisposeBattle();
        if (restoreDebugger && HotGameEntry.Debugger != null) HotGameEntry.Debugger.ActiveWindow = true;
    }

    private RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    private RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var rect = Rect(parent, name, x, y, w, h);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private TMP_Text Label(Transform parent, string name, string text, float x, float y, float w, float h,
        float size, Color color, bool center = false)
    {
        var label = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.raycastTarget = false;
        label.alignment = center ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        label.enableWordWrapping = true;
        return label;
    }

    private Button MakeButton(Transform parent, string name, string text, float x, float y, float w, float h,
        Action click, Color color, float size = 24)
    {
        var rect = Box(parent, name, x, y, w, h, color);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        Label(rect, "Label", text, 12, 4, w - 24, h - 8, size, Paper, true);
        button.onClick.AddListener(() => click());
        UIFormLogic.BindButtonClickSound(button);
        return button;
    }
}
