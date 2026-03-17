using UnityEngine;

/// <summary>
/// 镜头锚点平滑跟随机体位置，延迟约 0.05–0.1 秒，避免镜头抖动。
/// </summary>
public class CameraAnchorFollow : MonoBehaviour
{
    public Transform mech;
    [Tooltip("位置跟随的平滑时间（秒），0.05–0.1 较自然")]
    [Range(0.02f, 0.2f)]
    public float followSmoothTime = 0.08f;

    Vector3 _velocity;

    void LateUpdate()
    {
        if (mech == null) return;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            mech.position,
            ref _velocity,
            followSmoothTime,
            Mathf.Infinity,
            Time.deltaTime
        );
    }
}

