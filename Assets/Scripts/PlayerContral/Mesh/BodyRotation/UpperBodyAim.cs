using UnityEngine;

/// <summary>
/// 控制机甲上半身俯仰，使俯仰始终绕上下身连接点旋转，避免脱节与穿模。
/// </summary>
public class UpperBodyAim : MonoBehaviour
{
    [Header("参考")]
    [Tooltip("俯仰角来源（主相机或跟随相机的 pivot）")]
    public Transform cameraPivot;

    [Tooltip("下半身 Transform。不填则自动从同级查找 DownBody")]
    public Transform lowerBody;

    [Header("俯仰枢轴（解决脱节关键）")]
    [Tooltip("关节点在下半身局部空间中的偏移，随下半身旋转而移动，避免左右转时脱节。例如腰部在下半身正上方则填 (0, 正值, 0)")]
    public Vector3 jointOffsetFromLowerBody = Vector3.zero;

    [Tooltip("上半身本体上「连接点」相对本 Transform 的局部偏移。枢轴在正下方填 (0,-值,0)。为 (0,0,0) 时只旋转不移动")]
    public Vector3 pivotOffsetLocal = Vector3.zero;

    [Header("俯仰限制与平滑")]
    [Tooltip("俯仰角限制（度）")]
    public float pitchMin = -30f;
    public float pitchMax = 30f;

    [Tooltip("俯仰变化速度（度/秒）")]
    public float pitchSpeed = 90f;

    Transform _lowerBody;
    float _currentPitch;

    void Awake()
    {
        if (lowerBody == null)
            _lowerBody = transform.parent != null ? transform.parent.Find("DownBody") : null;
        else
            _lowerBody = lowerBody;
    }

    void Update()
    {
        if (cameraPivot == null || _lowerBody == null) return;

        float targetPitch = -Mathf.Asin(Mathf.Clamp(cameraPivot.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(targetPitch, pitchMin, pitchMax);

        _currentPitch = Mathf.MoveTowards(_currentPitch, targetPitch, pitchSpeed * Time.deltaTime);

        Quaternion newRotation = _lowerBody.rotation * Quaternion.Euler(_currentPitch, 0f, 0f);
        transform.rotation = newRotation;

        // 关节点始终从下半身计算，这样左右转向时关节点会随下半身移动，不会脱节
        if (pivotOffsetLocal != Vector3.zero)
        {
            Vector3 jointWorld = _lowerBody.TransformPoint(jointOffsetFromLowerBody);
            transform.position = jointWorld - transform.TransformDirection(pivotOffsetLocal);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (_lowerBody == null && lowerBody == null)
            _lowerBody = transform.parent != null ? transform.parent.Find("DownBody") : null;
        Transform lb = _lowerBody ?? lowerBody;
        Vector3 jointWorld = lb != null ? lb.TransformPoint(jointOffsetFromLowerBody) : transform.position;
        Vector3 pivotWorld = transform.position + transform.TransformDirection(pivotOffsetLocal);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pivotWorld, 0.08f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(jointWorld, 0.06f);
    }
}
