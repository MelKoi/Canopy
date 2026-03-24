using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家机甲资源：生命、韧性；被敌人子弹命中扣血并涨韧性，韧性满时全身闪红后恢复；生命为 0 销毁玩家。
/// </summary>
public class PlayerMechResources : MonoBehaviour
{
    [Header("生命")]
    public int maxHealth = 6000;
    public int healthDamagePerEnemyHit = 1000;

    [Header("韧性")]
    public int maxToughness = 100;
    public int toughnessGainPerEnemyHit = 20;

    [Header("视觉效果")]
    public Color toughnessFullTint = new Color(1f, 0.15f, 0.1f, 1f);
    public float toughnessFullFlashSeconds = 1f;

    [Tooltip("排除 UI；若为空则在 Awake 中按名称查找 FightUI")]
    public Transform uiRootToExclude;

    int _health;
    int _toughness;
    bool _flashRunning;
    readonly List<RendererColorSnapshot> _rendererSnapshots = new List<RendererColorSnapshot>();

    struct RendererColorSnapshot
    {
        public Renderer Renderer;
        public Color[] BaseColors;
    }

    public int CurrentHealth => _health;
    public int CurrentToughness => _toughness;
    public int MaxHealth => maxHealth;
    public int MaxToughness => maxToughness;

    void Awake()
    {
        _health = maxHealth;
        _toughness = 0;
        if (uiRootToExclude == null)
        {
            var t = transform.Find("FightUI");
            if (t != null)
                uiRootToExclude = t;
        }
        CacheRendererSnapshots();
    }

    void CacheRendererSnapshots()
    {
        _rendererSnapshots.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (uiRootToExclude != null && r.transform.IsChildOf(uiRootToExclude))
                continue;
            if (r is not MeshRenderer && r is not SkinnedMeshRenderer)
                continue;

            int count = r.sharedMaterials != null ? r.sharedMaterials.Length : 0;
            if (count == 0)
                continue;
            var cols = new Color[count];
            for (int i = 0; i < count; i++)
            {
                var m = r.sharedMaterials[i];
                if (m != null && m.HasProperty("_BaseColor"))
                    cols[i] = m.GetColor("_BaseColor");
                else if (m != null && m.HasProperty("_Color"))
                    cols[i] = m.GetColor("_Color");
                else
                    cols[i] = Color.white;
            }
            _rendererSnapshots.Add(new RendererColorSnapshot { Renderer = r, BaseColors = cols });
        }
    }

    /// <summary>被敌人子弹命中时调用。</summary>
    public void RegisterEnemyProjectileHit()
    {
        if (_health <= 0)
            return;

        _health -= healthDamagePerEnemyHit;
        _toughness = Mathf.Min(maxToughness, _toughness + toughnessGainPerEnemyHit);

        if (_health <= 0)
        {
            HandleDeath();
            return;
        }

        if (_toughness >= maxToughness && !_flashRunning)
            StartCoroutine(ToughnessFullFlashRoutine());
    }

    IEnumerator ToughnessFullFlashRoutine()
    {
        _flashRunning = true;
        ApplyTintAll(toughnessFullTint);
        yield return new WaitForSeconds(toughnessFullFlashSeconds);
        RestoreOriginalColors();
        _toughness = 0;
        _flashRunning = false;
    }

    void ApplyTintAll(Color c)
    {
        foreach (var snap in _rendererSnapshots)
        {
            var r = snap.Renderer;
            if (r == null || r.sharedMaterials == null)
                continue;
            for (int i = 0; i < snap.BaseColors.Length; i++)
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb, i);
                var m = i < r.sharedMaterials.Length ? r.sharedMaterials[i] : null;
                if (m != null)
                {
                    if (m.HasProperty("_BaseColor"))
                        mpb.SetColor("_BaseColor", c);
                    if (m.HasProperty("_Color"))
                        mpb.SetColor("_Color", c);
                    if (!m.HasProperty("_BaseColor") && !m.HasProperty("_Color"))
                        mpb.SetColor("_BaseColor", c);
                }
                else
                    mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(mpb, i);
            }
        }
    }

    void RestoreOriginalColors()
    {
        foreach (var snap in _rendererSnapshots)
        {
            var r = snap.Renderer;
            if (r == null)
                continue;
            for (int i = 0; i < snap.BaseColors.Length; i++)
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb, i);
                Color orig = snap.BaseColors[i];
                if (r.sharedMaterials != null && i < r.sharedMaterials.Length && r.sharedMaterials[i] != null)
                {
                    var m = r.sharedMaterials[i];
                    if (m.HasProperty("_BaseColor"))
                        mpb.SetColor("_BaseColor", orig);
                    if (m.HasProperty("_Color"))
                        mpb.SetColor("_Color", orig);
                    else
                        mpb.SetColor("_BaseColor", orig);
                }
                else
                {
                    mpb.SetColor("_BaseColor", orig);
                }
                r.SetPropertyBlock(mpb, i);
            }
        }
    }

    void HandleDeath()
    {
        DetachCameraRigFromHierarchy();
        Destroy(gameObject);
    }

    void DetachCameraRigFromHierarchy()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t == null)
                continue;
            if (t.name != "CameraRig")
                continue;
            t.SetParent(null, worldPositionStays: true);
            return;
        }
    }
}
