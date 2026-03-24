using System;
using UnityEngine;

/// <summary>
/// 挂在敌人上：累计命中达到阈值后变粉红并延迟销毁。
/// 若未挂此组件，子弹会按 Tag 走单次击杀的兜底逻辑。
/// </summary>
public class EnemyHitFeedback : MonoBehaviour
{
    [Tooltip("累计命中次数达到该值后变粉并销毁")]
    public int hitsToDestroy = 3;

    [Tooltip("变粉后延迟销毁时间（秒）")]
    public float destroyDelay = 1f;

    public static readonly Color HitColor = new Color(1f, 0.41f, 0.71f);

    public event Action OnFinalHitCommitted;

    int _hits;

    public void OnHit()
    {
        _hits++;
        if (_hits < hitsToDestroy)
            return;

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
