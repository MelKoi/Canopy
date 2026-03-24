using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将 PlayerMechResources 的生命值、韧性同步到 HealthBar / SynBar（与 EnergyBarUI 相同：用 Background 尺寸驱动 Fill 宽度）。
/// </summary>
public class PlayerHealthSynBarUI : MonoBehaviour
{
    public PlayerMechResources resources;

    [Header("HealthBarRoot")]
    public RectTransform healthBackgroundRect;
    public Image healthFillImage;

    [Header("SynBarRoot")]
    public RectTransform synBackgroundRect;
    public Image synFillImage;

    public float smoothSpeed = 10f;

    float _healthAmount = 1f;
    float _synAmount;

    void Start()
    {
        if (resources == null)
            resources = GetComponentInParent<PlayerMechResources>();

        if (healthFillImage != null)
            healthFillImage.type = Image.Type.Simple;
        if (synFillImage != null)
            synFillImage.type = Image.Type.Simple;

        if (resources != null)
        {
            _healthAmount = Mathf.Clamp01((float)resources.CurrentHealth / Mathf.Max(1, resources.MaxHealth));
            _synAmount = Mathf.Clamp01((float)resources.CurrentToughness / Mathf.Max(1, resources.MaxToughness));
        }
    }

    void Update()
    {
        if (resources == null)
            return;

        float targetHealth = resources.MaxHealth > 0
            ? Mathf.Clamp01((float)resources.CurrentHealth / resources.MaxHealth)
            : 0f;
        float targetSyn = resources.MaxToughness > 0
            ? Mathf.Clamp01((float)resources.CurrentToughness / resources.MaxToughness)
            : 0f;

        float dt = Time.deltaTime;
        _healthAmount = smoothSpeed <= 0f
            ? targetHealth
            : Mathf.MoveTowards(_healthAmount, targetHealth, smoothSpeed * dt);
        _synAmount = smoothSpeed <= 0f
            ? targetSyn
            : Mathf.MoveTowards(_synAmount, targetSyn, smoothSpeed * dt);

        ApplyBar(healthBackgroundRect, healthFillImage, _healthAmount);
        ApplyBar(synBackgroundRect, synFillImage, _synAmount);
    }

    static void ApplyBar(RectTransform backgroundRect, Image fillImage, float amount)
    {
        if (backgroundRect == null || fillImage == null)
            return;

        amount = Mathf.Clamp01(amount);
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
        fillRect.sizeDelta = new Vector2(barW * amount, barH);
    }
}
