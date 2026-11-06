using UnityEngine;

// Scales an authored 1920x1080 RectTransform inside the framework's existing UI canvas.
[ExecuteAlways]
public sealed class WorldTimelinePreparationLayout : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    private void OnEnable() => Fit();
    private void OnRectTransformDimensionsChange() => Fit();
    private void Fit()
    {
        if (content == null) return;
        var rect = ((RectTransform)transform).rect;
        float scale = Mathf.Min(rect.width / 1920, rect.height / 1080);
        if (scale > 0) content.localScale = Vector3.one * scale;
    }
}
