using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 根据 MechController 的当前能量更新体力条 Fill 的显示宽度。
/// 使用 Background 的尺寸和位置驱动 Fill 的 RectTransform，Fill 的 Image 使用 Simple 即可铺满条。
/// </summary>
public class EnergyBarUI : MonoBehaviour
{
    [Tooltip("提供能量数据的机甲控制器")]
    public MechController mech;
    [Tooltip("能量条的背景（用于取条的长度、高度和位置）")]
    public RectTransform backgroundRect;
    [Tooltip("用于显示填充的 Image（EnergyBarRoot 下的 Fill），Image Type 使用 Simple")]
    public Image fillImage;
    [Tooltip("填充变化的平滑速度")]
    public float smoothSpeed = 10f;

    float _currentAmount = 1f;

    void Start()
    {
        if (fillImage != null)
            fillImage.type = Image.Type.Simple;
        _currentAmount = mech != null && mech.MaxEnergy > 0f
            ? Mathf.Clamp01(mech.CurrentEnergy / mech.MaxEnergy)
            : 1f;
    }

    void Update()
    {
        if (mech == null || fillImage == null || backgroundRect == null)
            return;

        float targetAmount = mech.MaxEnergy > 0f
            ? Mathf.Clamp01(mech.CurrentEnergy / mech.MaxEnergy)
            : 0f;

        _currentAmount = smoothSpeed <= 0f
            ? targetAmount
            : Mathf.MoveTowards(_currentAmount, targetAmount, smoothSpeed * Time.deltaTime);

        Rect rect = backgroundRect.rect;
        float barW = rect.width;
        float barH = rect.height;
        float leftX = backgroundRect.anchoredPosition.x - barW * 0.5f;
        float centerY = backgroundRect.anchoredPosition.y;

        RectTransform fillRect = fillImage.rectTransform;
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.anchoredPosition = new Vector2(leftX, centerY);
        fillRect.sizeDelta = new Vector2(barW * _currentAmount, barH);
    }
}
