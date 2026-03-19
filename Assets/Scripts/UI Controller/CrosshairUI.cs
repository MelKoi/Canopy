using UnityEngine;

/// <summary>
/// 屏幕中心准星：将绑定的 RectTransform 固定在屏幕正中央。
/// 挂在 Canvas 下用于准星的 UI 物体上，并保证 Anchor 为中心。
/// </summary>
[ExecuteAlways]
public class CrosshairUI : MonoBehaviour
{
    [Tooltip("准星 UI，不填则用当前物体的 RectTransform")]
    public RectTransform crosshairRect;

    void Awake()
    {
        if (crosshairRect == null)
            crosshairRect = GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        if (crosshairRect == null) return;

        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRect.pivot = new Vector2(0.5f, 0.5f);
        crosshairRect.anchoredPosition = Vector2.zero;
    }
}
