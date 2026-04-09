using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用敌人巡逻路径配置：可挂在任意场景锚点（刷怪点、路点空物体、巡逻起点等）。
/// 路点为相对本物体 Transform 的本地坐标；运行时由生成逻辑把数据交给 <see cref="EnemyPatrolAgent"/>。
/// </summary>
public class EnemyPatrolPath : MonoBehaviour
{
    [Tooltip("相对本锚点 Transform 的本地坐标")]
    public List<PatrolWaypointEntry> waypoints = new List<PatrolWaypointEntry>();

    public float moveSpeed = 2.2f;

    [Tooltip("绕竖轴转向角速度（度/秒）")]
    public float turnSpeedDegrees = 360f;

    [Tooltip("视为到达路径点的距离阈值（世界空间）")]
    public float arriveDistance = 0.45f;

    public bool loop = true;

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        DrawPathGizmos(new Color(0.2f, 0.85f, 1f, 0.38f));
    }

    void OnDrawGizmosSelected()
    {
        DrawPathGizmos(new Color(0.2f, 0.85f, 1f, 0.92f));
    }

    void DrawPathGizmos(Color lineColor)
    {
        var t = transform;
        Gizmos.color = lineColor;
        Vector3 anchor = t.position;
        if (waypoints == null || waypoints.Count == 0)
        {
            Gizmos.DrawWireSphere(anchor, 0.25f);
            return;
        }

        Vector3 prev = anchor;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 wp = t.TransformPoint(waypoints[i].localPosition);
            Gizmos.DrawLine(prev, wp);
            Gizmos.DrawSphere(wp, 0.22f);
            prev = wp;
        }

        if (loop && waypoints.Count > 0)
        {
            Gizmos.color = new Color(lineColor.r, lineColor.g, lineColor.b, lineColor.a * 0.45f);
            Gizmos.DrawLine(prev, t.TransformPoint(waypoints[0].localPosition));
        }
    }
#endif
}
