using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Level_0 教学流程：在对应节点向已绑定的 TMP 写入文案并控制显隐。
/// 字体、颜色、字号等均在场景/预制体 Inspector 中配置。
/// 优先使用 <c>Story</c> 下的 <c>Sayer</c> / <c>Saying</c> / <c>Teach</c>（名称不区分大小写）。
/// </summary>
public class LevelTutorialStep1 : MonoBehaviour
{
    [Header("UI 引用")]
    public TextMeshProUGUI narrativeTitleText;
    public TextMeshProUGUI narrativeBodyText;
    [Tooltip("留空则分别控制标题/正文 GameObject 的显隐")]
    public GameObject narrativeRoot;

    public GameObject hintPanelRoot;
    public TextMeshProUGUI hintText;

    [Header("Timing（秒）")]
    public float delayBeforeNarrative = 2f;
    public float narrativeLine1Duration = 2f;
    public float narrativeLine2Duration = 2f;
    public float jumpHintDuration = 3f;

    [Header("开场叙事")]
    [TextArea] public string titleContent = "李秋烛";
    [TextArea] public string bodyLine1 = "代号长夜，现在已经将你秘密投放在工厂外围。";
    [TextArea] public string bodyLine2 = "寻找停泊在此处的飞行武器，将其破坏掉吧。";
    [TextArea] public string hintMovement = "使用w,a,s,d进行移动";
    [TextArea] public string hintJump = "使用空格进行跳跃";
    [TextArea] public string hintBoost = "按下ctrl进行加速推进。";

    [Header("浮空教学（FlyTeaching）")]
    [TextArea] public string flyTeachingSayer = "李秋烛";
    [TextArea] public string flyTeachingSaying = "能看到前面的平台了吗，迅速飞到那里去。";
    public float flyTeachingNarrativeSeconds = 2f;
    [TextArea] public string flyTeachingHint = "长按空格进行浮空。";
    [Tooltip("Teach 显示秒数；≤0 则保持显示，直至其它教学改写或关闭面板")]
    public float flyTeachingHintDuration;

    [Header("战斗教学（FightTeaching）")]
    [TextArea] public string fightTeachingOpenSayer = "李秋烛";
    [TextArea] public string fightTeachingOpenSaying = "看起来敌人还是安排了些许守卫，干掉他们吧。";
    public float fightTeachingOpenNarrativeSeconds = 2f;
    [TextArea] public string fightTeachingShootHint =
        "按下 Q、E、鼠标左键和鼠标右键进行射击，分别对应左肩武器、右肩武器、左手武器和右手武器。";
    [TextArea] public string fightTeachingReloadHint = "先按下 R，再按下对应位置可以进行换弹。";
    [TextArea] public string fightTeachingCompleteSayer = "李秋烛";
    [TextArea] public string fightTeachingCompleteSaying = "看来这边的门被锁死了，飞上去看看情况吧。";
    [Tooltip("最后一名敌人被击败后，稍候再弹出叙事，便于阅读换弹提示")]
    public float fightTeachingEndDelaySeconds = 1.6f;
    public float fightTeachingCompleteNarrativeSeconds = 3f;

    bool _jumpTeachHandled;
    bool _boostTeachHandled;
    Coroutine _jumpRoutine;
    bool _flyTeachHandled;
    Coroutine _flyRoutine;
    bool _fightTeachingHandled;
    Coroutine _fightTeachingRoutine;
    int _fightTeachingTotal;
    int _fightTeachingKills;
    bool _fightTeachingReloadHintShown;
    bool _fightTeachingCompleteStarted;
    PlayerGameplayInputGate _inputGate;
    bool _movementInputUnlocked;

    void Awake()
    {
        TryBindStoryUiIfNeeded();
        ApplyInitialUIState();
        LockPlayerInputUntilMovementTutorial();
    }

    void LockPlayerInputUntilMovementTutorial()
    {
        _inputGate = PlayerGameplayInputGate.FindOrCreate();
        _inputGate?.SetLocked(true);
    }

    void UnlockPlayerInputForMovementTutorial()
    {
        if (_movementInputUnlocked)
            return;
        _movementInputUnlocked = true;
        if (_inputGate == null)
            _inputGate = PlayerGameplayInputGate.FindOrCreate();
        _inputGate?.SetLocked(false);
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
        if (hintText == null && teachT != null)
        {
            var tmps = teachT.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (tmps.Length > 0)
                hintText = tmps[0];
        }
    }

    void ApplyInitialUIState()
    {
        HideNarrativeLines();

        if (hintPanelRoot != null)
            hintPanelRoot.SetActive(false);
    }

    void ShowNarrativeLines(string title, string body)
    {
        if (narrativeTitleText != null)
        {
            narrativeTitleText.text = title;
            narrativeTitleText.gameObject.SetActive(true);
        }

        if (narrativeBodyText != null)
        {
            narrativeBodyText.text = body;
            narrativeBodyText.gameObject.SetActive(true);
        }

        if (narrativeRoot != null)
            narrativeRoot.SetActive(true);
    }

    void UpdateNarrativeBodyLine(string body)
    {
        if (narrativeBodyText == null)
            return;
        narrativeBodyText.text = body;
    }

    void HideNarrativeLines()
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
    }

    void ShowHint(string text)
    {
        if (hintText != null)
            hintText.text = text;
        if (hintPanelRoot != null)
            hintPanelRoot.SetActive(true);
    }

    IEnumerator Start()
    {
        if (_inputGate == null)
            LockPlayerInputUntilMovementTutorial();

        yield return new WaitForSeconds(delayBeforeNarrative);

        ShowNarrativeLines(titleContent, bodyLine1);

        yield return new WaitForSeconds(narrativeLine1Duration);

        UpdateNarrativeBodyLine(bodyLine2);

        yield return new WaitForSeconds(narrativeLine2Duration);

        HideNarrativeLines();
        ShowHint(hintMovement);
        UnlockPlayerInputForMovementTutorial();
    }

    public void NotifyJumpTeachEntered()
    {
        if (_jumpTeachHandled)
            return;

        if (!_boostTeachHandled)
            return;

        _jumpTeachHandled = true;

        if (_flyRoutine != null)
        {
            StopCoroutine(_flyRoutine);
            _flyRoutine = null;
        }

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

        if (_flyRoutine != null)
        {
            StopCoroutine(_flyRoutine);
            _flyRoutine = null;
        }

        if (_jumpRoutine != null)
        {
            StopCoroutine(_jumpRoutine);
            _jumpRoutine = null;
        }

        ShowHint(hintBoost);
    }

    public void NotifyFlyTeachEntered()
    {
        if (_flyTeachHandled)
            return;
        _flyTeachHandled = true;

        if (_jumpRoutine != null)
        {
            StopCoroutine(_jumpRoutine);
            _jumpRoutine = null;
        }

        if (_flyRoutine != null)
            StopCoroutine(_flyRoutine);
        _flyRoutine = StartCoroutine(FlyTeachRoutine());
    }

    IEnumerator FlyTeachRoutine()
    {
        if (hintPanelRoot != null)
            hintPanelRoot.SetActive(false);

        ShowNarrativeLines(flyTeachingSayer, flyTeachingSaying);
        float narr = Mathf.Max(0.05f, flyTeachingNarrativeSeconds);
        yield return new WaitForSeconds(narr);
        HideNarrativeLines();

        ShowHint(flyTeachingHint);

        if (flyTeachingHintDuration > 0.001f)
        {
            yield return new WaitForSeconds(flyTeachingHintDuration);
            if (hintPanelRoot != null)
                hintPanelRoot.SetActive(false);
        }

        _flyRoutine = null;
    }

    public void NotifyFightTeachingEntered(IReadOnlyList<GameObject> fightTeachingEnemies)
    {
        if (_fightTeachingHandled)
            return;
        _fightTeachingHandled = true;

        if (_fightTeachingRoutine != null)
            StopCoroutine(_fightTeachingRoutine);
        _fightTeachingRoutine = StartCoroutine(FightTeachingFlowRoutine(fightTeachingEnemies));
    }

    IEnumerator FightTeachingFlowRoutine(IReadOnlyList<GameObject> fightTeachingEnemies)
    {
        _fightTeachingKills = 0;
        _fightTeachingReloadHintShown = false;
        _fightTeachingCompleteStarted = false;
        _fightTeachingTotal = 0;

        if (_flyRoutine != null)
        {
            StopCoroutine(_flyRoutine);
            _flyRoutine = null;
        }

        if (hintPanelRoot != null)
            hintPanelRoot.SetActive(false);

        ShowNarrativeLines(fightTeachingOpenSayer, fightTeachingOpenSaying);

        float openWait = Mathf.Max(0.05f, fightTeachingOpenNarrativeSeconds);
        yield return new WaitForSeconds(openWait);

        HideNarrativeLines();
        ShowHint(fightTeachingShootHint);

        if (fightTeachingEnemies == null || fightTeachingEnemies.Count == 0)
        {
            _fightTeachingRoutine = StartCoroutine(FightTeachingCompleteRoutine());
            yield break;
        }

        foreach (var go in fightTeachingEnemies)
        {
            if (go == null)
                continue;
            var fb = go.GetComponent<EnemyHitFeedback>();
            if (fb == null)
            {
                Debug.LogWarning("LevelTutorialStep1: 战斗教学敌人缺少 EnemyHitFeedback，无法统计击败。");
                continue;
            }

            _fightTeachingTotal++;
            EnemyHitFeedback captured = fb;
            Action handler = null;
            handler = () =>
            {
                captured.OnFinalHitCommitted -= handler;
                OnFightTeachingEnemyDefeated();
            };
            captured.OnFinalHitCommitted += handler;
        }

        if (_fightTeachingTotal == 0)
        {
            _fightTeachingRoutine = StartCoroutine(FightTeachingCompleteRoutine());
            yield break;
        }

        _fightTeachingRoutine = null;
        yield break;
    }

    void OnFightTeachingEnemyDefeated()
    {
        _fightTeachingKills++;

        if (_fightTeachingKills == 2 && !_fightTeachingReloadHintShown)
        {
            _fightTeachingReloadHintShown = true;
            if (hintText != null)
                hintText.text = fightTeachingReloadHint;
        }

        if (_fightTeachingKills < _fightTeachingTotal || _fightTeachingTotal <= 0 || _fightTeachingCompleteStarted)
            return;

        _fightTeachingCompleteStarted = true;
        if (_fightTeachingRoutine != null)
            StopCoroutine(_fightTeachingRoutine);
        _fightTeachingRoutine = StartCoroutine(FightTeachingCompleteRoutine());
    }

    IEnumerator FightTeachingCompleteRoutine()
    {
        float pre = Mathf.Max(0f, fightTeachingEndDelaySeconds);
        if (pre > 0.001f)
            yield return new WaitForSeconds(pre);

        if (hintPanelRoot != null)
            hintPanelRoot.SetActive(false);

        ShowNarrativeLines(fightTeachingCompleteSayer, fightTeachingCompleteSaying);

        float wait = Mathf.Max(0.05f, fightTeachingCompleteNarrativeSeconds);
        yield return new WaitForSeconds(wait);

        HideNarrativeLines();

        _fightTeachingRoutine = null;
    }

    IEnumerator JumpHintRoutine()
    {
        ShowHint(hintJump);

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
