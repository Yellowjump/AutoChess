using static WorldTimelineDebug.WorldTimelineDebugView;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using WorldTimelineDebug;

public sealed class WorldTimelinePreparationCtrl : UIFormLogic
{
    [Serializable] private sealed class CharacterSlot
    {
        public Button button;
        public Image portrait;
        public TMP_Text name, weapon;
        public CanvasGroup appearance;
        public GameObject selected, used;
        public Outline outline;
    }
    [Serializable] private sealed class HistorySlot
    {
        public Button button;
        public TMP_Text name, detail, state;
        public Image marker;
    }
    [SerializeField] private CharacterSlot[] characters;
    [SerializeField] private HistorySlot[] histories;
    [SerializeField] private TMP_Text cycle, selectedName, birthplace, weapon, bonus, historyCount, emptyText, pageLabel, issue;
    [SerializeField] private Image selectedPortrait;
    [SerializeField] private GameObject profile, emptyState;
    [SerializeField] private Button begin, returnMenu, clearHistories, previousPage, nextPage;
    private WorldTimelineDebugView view;
    private int page;
    private bool closing;
    public WorldTimelineDebugView Session => view;

    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            if (label.text.StartsWith("Timeline_", System.StringComparison.Ordinal)) label.text = TimelineText(label.text);
        for (int i = 0; i < characters.Length; i++)
        {
            int index = i;
            characters[i].button.onClick.AddListener(() => view?.SelectPreparationCharacter(index));
        }
        for (int i = 0; i < histories.Length; i++)
        {
            int slot = i;
            histories[i].button.onClick.AddListener(() =>
            {
                if (view == null) return;
                int index = page * histories.Length + slot;
                if (index < view.PreparationHistoryCount)
                    view.SelectPreparationHistory(index, !view.GetPreparationHistory(index).Selected);
            });
        }
        begin.onClick.AddListener(Begin);
        returnMenu.onClick.AddListener(() => view?.ReturnToMenu());
        clearHistories.onClick.AddListener(() => view?.ClearPreparationHistories());
        previousPage.onClick.AddListener(() => { page = Math.Max(0, page - 1); Refresh(); });
        nextPage.onClick.AddListener(() => { page++; Refresh(); });
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        closing = false;
        view = userData as WorldTimelineDebugView;
        if (view == null || !view.IsPreparing)
        {
            Debug.LogError("选人界面需要处于准备状态的轮回会话。");
            Close();
            return;
        }
        page = 0;
        view.Closed += Close;
        view.PreparationChanged += Refresh;
        if (view.PreparationSelectedIndex < 0)
            for (int i = 0; i < view.PreparationCharacterCount; i++)
                if (view.SelectPreparationCharacter(i)) break;
        Refresh();
    }

    public override void OnClose(bool isShutdown, object userData)
    {
        if (view != null)
        {
            view.Closed -= Close;
            view.PreparationChanged -= Refresh;
        }
        view = null;
        base.OnClose(isShutdown, userData);
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        if (view != null && !view.IsPreparing && view.UsesPrefabHud) Close();
    }

    private void Begin()
    {
        if (view == null || closing) return;
        begin.interactable = false;
        try
        {
            if (!view.BeginPreparedRun()) Refresh();
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            issue.text = TimelineText("Timeline_UI_82");
            begin.interactable = view.CanBeginPreparation;
        }
    }

    private void Close()
    {
        if (closing) return;
        closing = true;
        GameEntry.UI.CloseUIForm(UIForm);
    }

    private void Refresh()
    {
        if (view == null || closing) return;
        cycle.text = TimelineText("Timeline_UI_Cycle", view.PreparationCycle, view.PreparationAvailableCount, view.PreparationCharacterCount);
        for (int i = 0; i < characters.Length; i++)
        {
            var slot = characters[i];
            slot.button.gameObject.SetActive(i < view.PreparationCharacterCount);
            if (i >= view.PreparationCharacterCount) continue;
            var data = view.GetPreparationCharacter(i);
            slot.name.text = data.Name; slot.weapon.text = data.Weapon;
            slot.button.interactable = !data.Used;
            slot.appearance.alpha = data.Used ? .42f : 1;
            slot.selected.SetActive(data.Selected);
            slot.used.SetActive(data.Used);
            slot.outline.enabled = data.Selected;
        }
        int selected = view.PreparationSelectedIndex;
        profile.SetActive(selected >= 0);
        if (selected >= 0)
        {
            var data = view.GetPreparationCharacter(selected);
            selectedName.text = data.Name;
            birthplace.text = TimelineText("Timeline_UI_Birthplace", data.Birthplace, data.Q, data.R);
            weapon.text = TimelineText("Timeline_UI_StartingItem", data.Weapon);
            bonus.text = data.AttackBonus == 0 ? TimelineText("Timeline_UI_41") : TimelineText("Timeline_UI_StartingAttack", data.AttackBonus);
            selectedPortrait.sprite = characters[selected].portrait.sprite;
        }
        int total = view.PreparationHistoryCount;
        int pages = Math.Max(1, (total + histories.Length - 1) / histories.Length);
        page = Math.Min(page, pages - 1);
        emptyState.SetActive(total == 0);
        emptyText.text = TimelineText("Timeline_UI_32");
        historyCount.text = TimelineText("Timeline_UI_HistoryCount", view.PreparationSelectedCount);
        for (int i = 0; i < histories.Length; i++)
        {
            var slot = histories[i]; int index = page * histories.Length + i;
            slot.button.gameObject.SetActive(index < total);
            if (index >= total) continue;
            var data = view.GetPreparationHistory(index);
            slot.name.text = data.Name; slot.detail.text = data.CanSelect ? data.Detail : TimelineText("Timeline_UI_83");
            slot.button.interactable = data.CanSelect;
            slot.state.text = data.Selected ? TimelineText("Timeline_UI_84") : data.CanSelect ? TimelineText("Timeline_UI_63") : TimelineText("Timeline_UI_85");
            slot.marker.color = data.Selected ? new Color(.34f, .69f, .57f) : new Color(.56f, .51f, .39f);
        }
        pageLabel.text = $"{page + 1} / {pages}";
        previousPage.interactable = page > 0; nextPage.interactable = page + 1 < pages;
        previousPage.gameObject.SetActive(pages > 1); nextPage.gameObject.SetActive(pages > 1);
        pageLabel.gameObject.SetActive(pages > 1);
        clearHistories.interactable = view.PreparationSelectedCount > 0;
        issue.text = view.PreparationIssue ?? "";
        begin.interactable = view.CanBeginPreparation;
    }
}
