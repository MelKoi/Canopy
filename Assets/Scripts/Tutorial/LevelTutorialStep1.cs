using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡开场教学第一步：叙事（UI 上层）+ 左侧中部半透明紫面板提示。
/// 优先绑定场景根下 <c>Story</c> 中的 <c>Sayer</c> / <c>Saying</c> / <c>Teach</c>（名称不区分大小写）；
/// 若仍有缺引用且 <see cref="autoBuildUiWhenMissing"/> 为 true，再自动生成 UI。
/// Boost → Jump 覆盖顺序由触发器保证；Jump 文案显示 jumpHintDuration 秒后关闭面板。
/// </summary>
public class LevelTutorialStep1 : MonoBehaviour
{
    const int CanvasSortOrder = 5200;

    [Header("叙事（留空则运行时自动生成）")]
    public TextMeshProUGUI narrativeTitleText;
    public TextMeshProUGUI narrativeBodyText;
    [Tooltip("留空则自动生成；否则用于整块叙事显隐")]
    public GameObject narrativeRoot;

    [Header("左侧中部提示面板（留空则运行时自动生成）")]
    public GameObject hintPanelRoot;
    public TextMeshProUGUI hintText;

    [Header("运行时 UI")]
    [Tooltip("缺引用时在 Awake 中生成完整教程 UI")]
    public bool autoBuildUiWhenMissing = true;

    [Header("提示面板外观")]
    public Image hintPanelBackdrop;
    [Tooltip("底图：紫色半透明")]
    public Color hintBackdropColor = new Color(0.42f, 0.22f, 0.55f, 0.55f);
    public Color hintLabelColor = Color.white;

    [Header("叙事文字颜色（强制不透明）")]
    public Color narrativeTitleColor = Color.white;
    public Color narrativeBodyColor = new Color(0.96f, 0.96f, 0.98f, 1f);

    [Header("叙事区背景（自动生成时附加，便于阅读）")]
    public Color narrativeStripColor = new Color(0f, 0f, 0f, 0.38f);

    [Header("Timing（秒）")]
    public float delayBeforeNarrative = 2f;
    public float narrativeLine1Duration = 2f;
    public float narrativeLine2Duration = 2f;
    public float jumpHintDuration = 3f;

    [Header("文案")]
    [TextArea] public string titleContent = "李秋烛";
    [TextArea] public string bodyLine1 = "代号长夜，现在已经将你秘密投放在工厂外围。";
    [TextArea] public string bodyLine2 = "寻找停泊在此处的飞行武器，将其破坏掉吧。";
    [TextArea] public string hintMovement = "使用w,a,s,d进行移动";
    [TextArea] public string hintJump = "使用空格进行跳跃";
    [TextArea] public string hintBoost = "按下ctrl进行加速推进。";

    [Header("战斗教学触发（FightTeaching）")]
    [TextArea] public string fightTeachingSayer = "李秋烛";
    [TextArea] public string fightTeachingSaying = "看起来敌人还是安排了些许守卫，干掉他们吧。";
    public float fightTeachingNarrativeSeconds = 1f;

    bool _jumpTeachHandled;
    bool _boostTeachHandled;
    Coroutine _jumpRoutine;
    bool _runtimeGeneratedHintBackdrop;
    bool _fightTeachingHandled;
    Coroutine _fightTeachingRoutine;

    void Awake()
    {
        TryBindStoryUiIfNeeded();
        if (autoBuildUiWhenMissing)
            EnsureRuntimeUi();

        ApplyHintPanelChrome();
        ApplyInitialUIState();
    }

    void TryBindStoryUiIfNeeded()
    {
        bool needAny = narrativeTitleText == null || narrativeBodyText == null
                       || hintPanelRoot == null || hintText == null;
        if (!needAny)
            return;

        Scene s = gameObject.scene;
        if (!s.IsValid())
            return;

        Transform story = null;
        foreach (var root in s.GetRootGameObjects())
        {
            story = FindTransformByNameRecursive(root.transform, "Story");
            if (story != null)
                break;
        }

        if (story == null)
            return;

        Transform sayerT = FindTransformByNameRecursive(story, "Sayer");
        Transform sayingT = FindTransformByNameRecursive(story, "Saying");
        Transform teachT = FindTransformByNameRecursive(story, "Teach");

        if (narrativeTitleText == null && sayerT != null)
            narrativeTitleText = sayerT.GetComponent<TextMeshProUGUI>()
                                 ?? sayerT.GetComponentInChildren<TextMeshProUGUI>(true);
        if (narrativeBodyText == null && sayingT != null)
            narrativeBodyText = sayingT.GetComponent<TextMeshProUGUI>()
                                ?? sayingT.GetComponentInChildren<TextMeshProUGUI>(true);
        if (hintPanelRoot == null && teachT != null)
            hintPanelRoot = teachT.gameObject;
        if (hintPanelBackdrop == null && teachT != null)
            hintPanelBackdrop = teachT.GetComponent<Image>();
        if (hintText == null && teachT != null)
        {
            var tmps = teachT.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                hintText = tmps[i];
                break;
            }
        }
    }

    void EnsureRuntimeUi()
    {
        bool need = narrativeTitleText == null || narrativeBodyText == null
                    || hintPanelRoot == null || hintText == null;
        if (!need)
            return;

        EnsureEventSystem();

        var canvasGo = new GameObject("TutorialUICanvas");
        Transform uiParent = transform;
        Scene s = gameObject.scene;
        if (s.IsValid())
        {
            foreach (GameObject root in s.GetRootGameObjects())
            {
                Transform t = FindTransformByNameRecursive(root.transform, "Story");
                if (t != null)
                {
                    uiParent = t;
                    break;
                }
            }
            if (uiParent == transform)
            {
                foreach (GameObject root in s.GetRootGameObjects())
                {
                    Transform t = FindTransformByNameRecursive(root.transform, "Teaching");
                    if (t != null)
                    {
                        uiParent = t;
                        break;
                    }
                }
            }
        }
        canvasGo.transform.SetParent(uiParent, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortOrder;
        canvas.overrideSorting = true;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        TMP_FontAsset font = GetTmpFont();

        // —— 叙事区：上半屏居中，标题 + 正文
        narrativeRoot = new GameObject("NarrativeRoot");
        var nRootRt = narrativeRoot.AddComponent<RectTransform>();
        narrativeRoot.transform.SetParent(canvasGo.transform, false);
        nRootRt.anchorMin = new Vector2(0.06f, 0.48f);
        nRootRt.anchorMax = new Vector2(0.94f, 0.94f);
        nRootRt.offsetMin = Vector2.zero;
        nRootRt.offsetMax = Vector2.zero;

        var narrStrip = new GameObject("NarrativeStrip");
        var stripRt = narrStrip.AddComponent<RectTransform>();
        narrStrip.transform.SetParent(narrativeRoot.transform, false);
        stripRt.anchorMin = Vector2.zero;
        stripRt.anchorMax = Vector2.one;
        stripRt.offsetMin = Vector2.zero;
        stripRt.offsetMax = Vector2.zero;
        var stripImg = narrStrip.AddComponent<Image>();
        stripImg.color = narrativeStripColor;
        stripImg.raycastTarget = false;

        GameObject titleGo = CreateTmpObject("NarrativeTitle", narrativeRoot.transform, font, 40f,
            TextAlignmentOptions.Center, true);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -24f);
        titleRt.sizeDelta = new Vector2(880f, 72f);
        narrativeTitleText = titleGo.GetComponent<TextMeshProUGUI>();

        GameObject bodyGo = CreateTmpObject("NarrativeBody", narrativeRoot.transform, font, 26f,
            TextAlignmentOptions.Top | TextAlignmentOptions.Center, true);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.5f, 1f);
        bodyRt.anchorMax = new Vector2(0.5f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 1f);
        bodyRt.anchoredPosition = new Vector2(0f, -100f);
        bodyRt.sizeDelta = new Vector2(900f, 220f);
        narrativeBodyText = bodyGo.GetComponent<TextMeshProUGUI>();

        // —— 左侧中部紫半透明面板
        hintPanelRoot = new GameObject("HintPanelRoot");
        var hRootRt = hintPanelRoot.AddComponent<RectTransform>();
        hintPanelRoot.transform.SetParent(canvasGo.transform, false);
        hRootRt.anchorMin = new Vector2(0f, 0.38f);
        hRootRt.anchorMax = new Vector2(0f, 0.62f);
        hRootRt.pivot = new Vector2(0f, 0.5f);
        hRootRt.anchoredPosition = new Vector2(32f, 0f);
        hRootRt.sizeDelta = new Vector2(412f, 156f);

        var backdropGo = new GameObject("HintBackdrop");
        var bdRt = backdropGo.AddComponent<RectTransform>();
        backdropGo.transform.SetParent(hintPanelRoot.transform, false);
        bdRt.anchorMin = Vector2.zero;
        bdRt.anchorMax = Vector2.one;
        bdRt.offsetMin = Vector2.zero;
        bdRt.offsetMax = Vector2.zero;
        hintPanelBackdrop = backdropGo.AddComponent<Image>();
        hintPanelBackdrop.sprite = null;
        hintPanelBackdrop.type = Image.Type.Simple;
        hintPanelBackdrop.color = hintBackdropColor;
        hintPanelBackdrop.raycastTarget = false;
        _runtimeGeneratedHintBackdrop = true;

        GameObject hintGo = CreateTmpObject("HintLabel", hintPanelRoot.transform, font, 24f,
            TextAlignmentOptions.MidlineLeft, false);
        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = Vector2.zero;
        hintRt.anchorMax = Vector2.one;
        hintRt.offsetMin = new Vector2(18f, 14f);
        hintRt.offsetMax = new Vector2(-18f, -14f);
        hintText = hintGo.GetComponent<TextMeshProUGUI>();
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include) != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    static TMP_FontAsset GetTmpFont()
    {
        if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;
        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    static GameObject CreateTmpObject(string name, Transform parent, TMP_FontAsset font, float size,
        TextAlignmentOptions align, bool enableWordWrapping)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.textWrappingMode = enableWordWrapping ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.color = Color.white;
        return go;
    }

    void ApplyHintPanelChrome()
    {
        if (hintPanelBackdrop != null && _runtimeGeneratedHintBackdrop)
            hintPanelBackdrop.color = hintBackdropColor;

        if (hintText != null)
            hintText.color = Opaque(hintLabelColor);
    }

    static Color Opaque(Color c)
    {
        c.a = 1f;
        return c;
    }

    void ApplyInitialUIState()
    {
        if (narrativeRoot != null)
            narrativeRoot.SetActive(false);
        else
        {
            if (narrativeTitleText != null)
                narrativeTitleText.gameObject.SetActive(false);
            if (narrativeBodyText != null)
                narrativeBodyText.gameObject.SetActive(false);
        }

        if (hintPanelRoot != null)
            hintPanelRoot.SetActive(false);
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(delayBeforeNarrative);

        if (narrativeTitleText != null)
        {
            narrativeTitleText.text = titleContent;
            narrativeTitleText.color = Opaque(narrativeTitleColor);
            narrativeTitleText.gameObject.SetActive(true);
        }

        if (narrativeBodyText != null)
        {
            narrativeBodyText.text = bodyLine1;
            narrativeBodyText.color = Opaque(narrativeBodyColor);
            narrativeBodyText.gameObject.SetActive(true);
        }

        if (narrativeRoot != null)
            narrativeRoot.SetActive(true);

        yield return new WaitForSeconds(narrativeLine1Duration);

        if (narrativeBodyText != null)
            narrativeBodyText.text = bodyLine2;

        yield return new WaitForSeconds(narrativeLine2Duration);

        if (narrativeRoot != null)
            narrativeRoot.SetActive(false);
        else
        {
            if (narrativeTitleText != null)
                narrativeTitleText.gameObject.SetActive(false);
            if (narrativeBodyText != null)
                narrativeBodyText.gameObject.SetActive(false);
        }

        if (hintText != null)
        {
            hintText.text = hintMovement;
            hintText.color = Opaque(hintLabelColor);
        }

        if (hintPanelRoot != null)
            hintPanelRoot.SetActive(true);
    }

    public void NotifyJumpTeachEntered()
    {
        if (_jumpTeachHandled)
            return;

        if (!_boostTeachHandled)
            return;

        _jumpTeachHandled = true;

        if (_jumpRoutine != null)
            StopCoroutine(_jumpRoutine);

        _jumpRoutine = StartCoroutine(JumpHintRoutine());
    }

    public void NotifyBoostTeachEntered()
    {
        if (_boostTeachHandled)
            return;

        if (_jumpTeachHandled)
            return;

        _boostTeachHandled = true;

        if (_jumpRoutine != null)
        {
            StopCoroutine(_jumpRoutine);
            _jumpRoutine = null;
        }

        if (hintPanelRoot != null && !hintPanelRoot.activeSelf)
            hintPanelRoot.SetActive(true);

        if (hintText != null)
        {
            hintText.text = hintBoost;
            hintText.color = Opaque(hintLabelColor);
        }
    }

    public void NotifyFightTeachingEntered()
    {
        if (_fightTeachingHandled)
            return;
        _fightTeachingHandled = true;

        if (_fightTeachingRoutine != null)
            StopCoroutine(_fightTeachingRoutine);
        _fightTeachingRoutine = StartCoroutine(FightTeachingNarrativeRoutine());
    }

    IEnumerator FightTeachingNarrativeRoutine()
    {
        if (narrativeTitleText != null)
        {
            narrativeTitleText.text = fightTeachingSayer;
            narrativeTitleText.color = Opaque(narrativeTitleColor);
            narrativeTitleText.gameObject.SetActive(true);
        }

        if (narrativeBodyText != null)
        {
            narrativeBodyText.text = fightTeachingSaying;
            narrativeBodyText.color = Opaque(narrativeBodyColor);
            narrativeBodyText.gameObject.SetActive(true);
        }

        if (narrativeRoot != null)
            narrativeRoot.SetActive(true);

        float wait = Mathf.Max(0.05f, fightTeachingNarrativeSeconds);
        yield return new WaitForSeconds(wait);

        if (narrativeRoot != null)
            narrativeRoot.SetActive(false);
        else
        {
            if (narrativeTitleText != null)
                narrativeTitleText.gameObject.SetActive(false);
            if (narrativeBodyText != null)
                narrativeBodyText.gameObject.SetActive(false);
        }

        _fightTeachingRoutine = null;
    }

    IEnumerator JumpHintRoutine()
    {
        if (hintPanelRoot != null && !hintPanelRoot.activeSelf)
            hintPanelRoot.SetActive(true);

        if (hintText != null)
        {
            hintText.text = hintJump;
            hintText.color = Opaque(hintLabelColor);
        }

        yield return new WaitForSeconds(jumpHintDuration);

        if (hintPanelRoot != null)
            hintPanelRoot.SetActive(false);

        _jumpRoutine = null;
    }

    static Transform FindTransformByNameRecursive(Transform t, string objectName)
    {
        if (string.Equals(t.name, objectName, StringComparison.OrdinalIgnoreCase))
            return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform found = FindTransformByNameRecursive(t.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }
}
