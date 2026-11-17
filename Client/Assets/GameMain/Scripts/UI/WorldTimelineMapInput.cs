using UnityEngine;
using UnityEngine.EventSystems;
using WorldTimelineDebug;

public sealed class WorldTimelineMapInput : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IScrollHandler, IPointerMoveHandler, IPointerExitHandler
{
    [SerializeField] private WorldTimelinePanelCtrl owner;
    private bool dragged;
    public void OnPointerDown(PointerEventData data) { dragged = false; }
    public void OnPointerClick(PointerEventData data)
    {
        if (!dragged && data.button == PointerEventData.InputButton.Left) owner.SelectNode(data.position);
    }
    public void OnBeginDrag(PointerEventData data) { dragged = true; owner.ClearHover(); }
    public void OnDrag(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left) owner.Pan(data.delta);
    }
    public void OnScroll(PointerEventData data) => owner.Zoom(WorldTimelineBoard.WheelZoomFactor(-data.scrollDelta.y));
    public void OnPointerMove(PointerEventData data) { if (!dragged) owner.HoverNode(data.position); }
    public void OnPointerExit(PointerEventData data) => owner.ClearHover();
}
