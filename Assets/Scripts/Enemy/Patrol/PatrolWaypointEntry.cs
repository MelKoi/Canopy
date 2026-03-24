using System;
using UnityEngine;

/// <summary>
/// 巡逻路径上的一点：坐标为相对锚点（挂 <see cref="EnemyPatrolPath"/> 的 Transform）的本地位置，到达后等待若干秒。
/// </summary>
[Serializable]
public class PatrolWaypointEntry
{
    public Vector3 localPosition;

    [Tooltip("到达该点后停留秒数再走向下一点")]
    public float waitSeconds = 0.35f;
}
