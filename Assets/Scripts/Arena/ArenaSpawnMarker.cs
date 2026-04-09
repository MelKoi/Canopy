using UnityEngine;

/// <summary>
/// 仅在 Scene 视图中绘制线框球体，便于在编辑状态下看见出生点空物体位置（不依赖 Play）。
/// </summary>
public class ArenaSpawnMarker : MonoBehaviour
{
    public Color gizmoColor = Color.yellow;
    [Min(0.1f)] public float radius = 0.75f;

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
