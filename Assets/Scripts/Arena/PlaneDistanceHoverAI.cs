using UnityEngine;

/// <summary>
/// 飞行敌人：保持与玩家机甲 <c>Mesh</c> 子物体约 <see cref="preferredDistanceMeters"/> 的直线距离（默认 20m），并叠加轻微竖直漂浮。
/// 仅绕世界 Y 轴旋转使机体水平朝向玩家。与非玩家碰撞时优先抬升高度，并暂时允许与玩家距离放宽到 <see cref="preferredDistanceExpandedMeters"/>（默认 25m）。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlaneDistanceHoverAI : MonoBehaviour
{
    [SerializeField] string playerMeshChildName = "Mesh";
    [Tooltip("代表机头的空物体；未赋值时自动查找名为 front 的子物体")]
    [SerializeField] Transform front;

    [Min(0.5f)] public float preferredDistanceMeters = 20f;
    [Tooltip("与环境碰撞避让期间，与玩家允许的最大直线距离")]
    [Min(0.5f)] public float preferredDistanceExpandedMeters = 25f;
    [Min(0.05f)] public float positionSmoothTime = 0.42f;
    [Min(0.1f)] public float maxMoveSpeed = 28f;

    [Header("漂浮")]
    public float bobVerticalAmplitude = 0.22f;
    public float bobVerticalFrequencyHz = 0.38f;

    [Header("朝向")]
    [Tooltip("与玩家 Mesh 距离小于等于此值时，视为接战：水平朝向玩家（供射击与朝向共用）")]
    public float engagementDistanceMeters = 32f;
    public float turnLerp = 8f;

    [Header("环境碰撞避让")]
    [Tooltip("撞到非玩家后，在若干秒内启用抬升 + 放宽距离")]
    public float obstacleAvoidBoostSeconds = 3f;
    [Tooltip("避让期间目标高度相对当前机体每秒抬升（米/秒），与 SmoothDamp 叠加")]
    public float obstacleLiftMetersPerSecond = 6f;
    [Tooltip("避让期间额外加在目标高度上的偏置（米）")]
    public float obstacleLiftHeightBias = 4f;

    /// <summary>本帧是否处于接战距离内（供 <see cref="PlaneCombat"/> 使用）。</summary>
    public bool PlayerInEngagementRange { get; private set; }

    Transform _playerMesh;
    MechController _playerMech;
    Vector3 _smoothVel;
    float _bobPhase;
    float _avoidEnvUntil;

    void Awake()
    {
        _bobPhase = Random.Range(0f, Mathf.PI * 2f);
        if (front == null)
            front = FindChildDepthFirst(transform, "front");

        var rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    void LateUpdate()
    {
        if (!EnsurePlayerMesh())
            return;

        Vector3 p = _playerMesh.position;
        Vector3 e = transform.position;
        float engagementDist = front != null ? Vector3.Distance(p, front.position) : Vector3.Distance(p, e);
        PlayerInEngagementRange = engagementDist <= engagementDistanceMeters;

        bool avoidLift = Time.time < _avoidEnvUntil;
        float holdDist = avoidLift ? preferredDistanceExpandedMeters : preferredDistanceMeters;

        Vector3 delta = e - p;
        float dist = delta.magnitude;
        Vector3 dir = dist > 0.02f ? delta / dist : FlatForwardOrFallback();

        Vector3 ring = p + dir * holdDist;
        float bob = Mathf.Sin((Time.time * bobVerticalFrequencyHz * (Mathf.PI * 2f)) + _bobPhase) * bobVerticalAmplitude;
        ring.y += bob;
        if (avoidLift)
        {
            ring.y = Mathf.Max(ring.y, e.y + obstacleLiftMetersPerSecond * Time.deltaTime);
            ring.y = Mathf.Max(ring.y, p.y + obstacleLiftHeightBias);
        }

        transform.position = Vector3.SmoothDamp(transform.position, ring, ref _smoothVel, positionSmoothTime, maxMoveSpeed,
            Time.deltaTime);

        if (PlayerInEngagementRange)
            ApplyYawOnlyTowardPlayer(p);
    }

    void OnCollisionEnter(Collision collision) => HandleObstacleContact(collision != null ? collision.gameObject : null);

    void OnTriggerEnter(Collider other) => HandleObstacleContact(other != null ? other.gameObject : null);

    void HandleObstacleContact(GameObject other)
    {
        if (other == null)
            return;
        if (IsPlayerHierarchy(other))
            return;
        _avoidEnvUntil = Time.time + Mathf.Max(0.2f, obstacleAvoidBoostSeconds);
    }

    bool IsPlayerHierarchy(GameObject go)
    {
        if (_playerMech == null)
            _playerMech = FindFirstObjectByType<MechController>();
        if (_playerMech == null)
            return false;
        Transform t = go.transform;
        return t == _playerMech.transform || t.IsChildOf(_playerMech.transform);
    }

    void ApplyYawOnlyTowardPlayer(Vector3 playerPos)
    {
        Vector3 pivot = front != null ? front.position : transform.position;
        Vector3 flat = playerPos - pivot;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f)
            return;
        flat.Normalize();

        Quaternion want = Quaternion.LookRotation(flat, Vector3.up);
        float targetY = want.eulerAngles.y;
        float y = transform.eulerAngles.y;
        float newY = Mathf.LerpAngle(y, targetY, 1f - Mathf.Exp(-turnLerp * Time.deltaTime));
        transform.rotation = Quaternion.Euler(0f, newY, 0f);
    }

    Vector3 FlatForwardOrFallback()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    bool EnsurePlayerMesh()
    {
        if (_playerMesh != null)
            return true;

        var mech = FindFirstObjectByType<MechController>();
        if (mech == null)
            return false;

        _playerMech = mech;

        var mesh = mech.transform.Find(playerMeshChildName);
        if (mesh == null)
            mesh = FindChildDepthFirst(mech.transform, playerMeshChildName);
        _playerMesh = mesh != null ? mesh : mech.transform;
        return _playerMesh != null;
    }

    static Transform FindChildDepthFirst(Transform root, string targetName)
    {
        if (root == null)
            return null;
        if (root.name == targetName)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindChildDepthFirst(root.GetChild(i), targetName);
            if (hit != null)
                return hit;
        }

        return null;
    }
}
