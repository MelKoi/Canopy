using UnityEngine;

/// <summary>
/// 单目标锁定：当敌人进入准星附近一定距离（视口半径）时锁定，敌人身上由 LockOnUI 显示锁定框。
/// </summary>
public class LockOnSystem : MonoBehaviour
{
    public Camera cam;
    [Tooltip("准星中心 (0.5,0.5) 周围的锁定半径（视口 0~1），敌人在此范围内即锁定")]
    [Range(0.02f, 0.3f)]
    public float lockRadiusViewport = 0.08f;
    [Tooltip("敌人 Tag，用于检测可锁定目标")]
    public string enemyTag = "Enemy";
    [Tooltip("检测敌人的世界空间半径")]
    public float detectRadius = 100f;

    public Transform currentTarget { get; private set; }

    float LockRadiusSq => lockRadiusViewport * lockRadiusViewport;

    float ViewportDistSqFromCenter(Vector3 viewportPos)
    {
        float dx = viewportPos.x - 0.5f;
        float dy = viewportPos.y - 0.5f;
        return dx * dx + dy * dy;
    }

    void Update()
    {
        if (currentTarget == null)
            DetectEnemy();
    }

    void LateUpdate()
    {
        if (currentTarget == null) return;

        Vector3 viewportPos = cam.WorldToViewportPoint(currentTarget.position);
        if (viewportPos.z < 0)
        {
            currentTarget = null;
            return;
        }

        float dx = viewportPos.x - 0.5f;
        float dy = viewportPos.y - 0.5f;
        if (dx * dx + dy * dy > lockRadiusViewport * lockRadiusViewport)
            currentTarget = null;
    }

    void DetectEnemy()
    {
        Collider[] all = Physics.OverlapSphere(transform.position, detectRadius);
        float r2 = LockRadiusSq;
        float bestDist2 = float.MaxValue;
        Transform best = null;

        foreach (var c in all)
        {
            if (!c.CompareTag(enemyTag)) continue;

            Vector3 vp = cam.WorldToViewportPoint(c.transform.position);
            if (vp.z < 0) continue;

            float d2 = ViewportDistSqFromCenter(vp);
            if (d2 <= r2 && d2 < bestDist2)
            {
                bestDist2 = d2;
                best = c.transform;
            }
        }

        if (best != null)
            currentTarget = best;
    }
}

