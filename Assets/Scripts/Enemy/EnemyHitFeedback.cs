using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 挂在敌人上：具有最大生命值，累计受到的伤害达到上限后变粉红并延迟销毁。
/// 若未挂此组件，子弹会按 Tag 走单次击杀的兜底逻辑。
/// </summary>
public class EnemyHitFeedback : MonoBehaviour
{
    [FormerlySerializedAs("hitsToDestroy")]
    [Tooltip("最大生命值；累计承受伤害达到该值后销毁")]
    public float maxHealth = 100f;

    [Tooltip("变粉后延迟销毁时间（秒）")]
    public float destroyDelay = 1f;

    public static readonly Color HitColor = new Color(1f, 0.41f, 0.71f);

    public event Action OnFinalHitCommitted;

    /// <summary>每次扣血后触发（当前生命, 最大生命）。</summary>
    public event Action<float, float> OnHealthChanged;

    float _currentHealth;
    bool _finalized;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => _currentHealth;

    void Awake()
    {
        maxHealth = Mathf.Max(1e-4f, maxHealth);
        _currentHealth = maxHealth;
    }

    /// <summary>将当前生命重置为满（用于生成/复用时手动调用）。</summary>
    public void ResetHealthToFull()
    {
        maxHealth = Mathf.Max(1e-4f, maxHealth);
        _currentHealth = maxHealth;
        _finalized = false;
    }

    public void ApplyDamage(float damage)
    {
        if (_finalized || damage <= 0f)
            return;

        _currentHealth -= damage;
        if (_currentHealth < 0f)
            _currentHealth = 0f;

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        if (_currentHealth > 1e-4f)
            return;

        CommitFinalDeath();
    }

    void CommitFinalDeath()
    {
        if (_finalized)
            return;
        _finalized = true;
        OnFinalHitCommitted?.Invoke();
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r.material != null)
            {
                var mat = new Material(r.material);
                mat.color = HitColor;
                r.material = mat;
            }
        }

        Destroy(gameObject, destroyDelay);
    }
}
