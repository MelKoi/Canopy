using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 与 <see cref="EnemyHitFeedback"/> 同挂 Plane：按累计损失生命值（真实伤害）跨过阈值时在屏幕中上弹出播报。
/// </summary>
[RequireComponent(typeof(EnemyHitFeedback))]
public class PlaneHealthMilestoneAnnouncer : MonoBehaviour
{
    [SerializeField] EnemyHitFeedback hitFeedback;
    [FormerlySerializedAs("milestoneEveryHits")]
    [Min(0.001f)]
    public float milestoneEveryDamage = 1000f;
    [TextArea] public string announceMessage = "血量已经下降1000";
    public float showDurationSeconds = 2.5f;
    public int toastSortOrder = 4100;

    Canvas _toastCanvas;
    TextMeshProUGUI _toastText;
    Coroutine _hideRoutine;
    int _announcedMilestoneCount;

    void Awake()
    {
        if (hitFeedback == null)
            hitFeedback = GetComponent<EnemyHitFeedback>();
        milestoneEveryDamage = Mathf.Max(0.001f, milestoneEveryDamage);
        _announcedMilestoneCount = 0;
    }

    void OnEnable()
    {
        if (hitFeedback != null)
        {
            hitFeedback.OnHealthChanged += OnHealthChanged;
            _announcedMilestoneCount = 0;
        }
    }

    void OnDisable()
    {
        if (hitFeedback != null)
            hitFeedback.OnHealthChanged -= OnHealthChanged;
        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }
    }

    void OnHealthChanged(float currentHealth, float maxHealth)
    {
        if (maxHealth <= 0f)
            return;
        float taken = maxHealth - currentHealth;
        int crossed = Mathf.FloorToInt(taken / milestoneEveryDamage);
        while (_announcedMilestoneCount < crossed)
        {
            _announcedMilestoneCount++;
            ShowToast(announceMessage);
        }
    }

    void ShowToast(string message)
    {
        EnsureToastUi();
        if (_toastText == null)
            return;
        _toastText.text = message;
        _toastText.gameObject.SetActive(true);
        if (_toastCanvas != null)
            _toastCanvas.gameObject.SetActive(true);

        if (_hideRoutine != null)
            StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, showDurationSeconds));
        if (_toastText != null)
            _toastText.gameObject.SetActive(false);
        if (_toastCanvas != null)
            _toastCanvas.gameObject.SetActive(false);
        _hideRoutine = null;
    }

    void EnsureToastUi()
    {
        if (_toastText != null)
            return;

        var canvasGo = new GameObject("PlaneMilestoneToast");
        if (gameObject.scene.IsValid())
            SceneManager.MoveGameObjectToScene(canvasGo, gameObject.scene);
        _toastCanvas = canvasGo.AddComponent<Canvas>();
        _toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _toastCanvas.sortingOrder = toastSortOrder;
        _toastCanvas.overrideSorting = true;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var labelGo = new GameObject("Label");
        var rt = labelGo.AddComponent<RectTransform>();
        rt.SetParent(canvasGo.transform, false);
        rt.anchorMin = new Vector2(0.15f, 0.62f);
        rt.anchorMax = new Vector2(0.85f, 0.78f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _toastText = labelGo.AddComponent<TextMeshProUGUI>();
        var font = TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null
            ? TMP_Settings.defaultFontAsset
            : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null)
            _toastText.font = font;
        _toastText.fontSize = 36f;
        _toastText.alignment = TextAlignmentOptions.Center;
        _toastText.textWrappingMode = TextWrappingModes.Normal;
        _toastText.raycastTarget = false;
        _toastText.color = new Color(1f, 0.92f, 0.35f, 1f);
        _toastText.gameObject.SetActive(false);
    }
}
