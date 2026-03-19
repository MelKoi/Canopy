using UnityEngine;

/// <summary>
/// 挂在敌人上：被子弹击中后材质变粉红色，1 秒后销毁。
/// 若未挂此组件，子弹会按 layer 自动执行相同逻辑。
/// </summary>
public class EnemyHitFeedback : MonoBehaviour
{
    [Tooltip("击中后延迟销毁时间（秒）")]
    public float destroyDelay = 1f;

    static readonly Color HitColor = new Color(1f, 0.41f, 0.71f);

    public void OnHit()
    {
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
