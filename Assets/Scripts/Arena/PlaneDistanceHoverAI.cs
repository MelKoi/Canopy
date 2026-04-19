using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 飞行敌人：在水平面内与玩家 <c>Mesh</c> 保持约 <see cref="preferredHorizontalDistanceMeters"/> 的环绕距离，
/// 目标高度为玩家高度 + <see cref="heightAbovePlayerMeters"/>，并叠加轻微竖直漂浮。
/// 与纯三维直线距离相比，可避免玩家缩在机腹正下方时飞机仍悬在正上方导致难以命中。
/// 与非玩家碰撞时优先抬升高度，并暂时允许水平距离放宽到 <see cref="preferredHorizontalDistanceExpandedMeters"/>。
/// 目标位置在应用 SmoothDamp 前经 <see cref="ClampDestinationAgainstStaticGeometry"/> 约束，减轻运动学位移穿墙。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlaneDistanceHoverAI : MonoBehaviour
{
    [SerializeField] string playerMeshChildName = "Mesh";
    [Tooltip("代表机头的空物体；未赋值时自动查找名为 front 的子物体")]
    [SerializeField] Transform front;

    [FormerlySerializedAs("preferredDistanceMeters")]
    [Min(0.5f)]
    [Tooltip("与玩家在水平面（XZ）上保持的环绕距离（米），略大可减少被绕到正下方")]
    public float preferredHorizontalDistanceMeters = 38f;
    [FormerlySerializedAs("preferredDistanceExpandedMeters")]
    [Tooltip("与环境碰撞避让期间，水平环绕距离的上限（米）")]
    [Min(0.5f)]
    public float preferredHorizontalDistanceExpandedMeters = 44f;
    [Tooltip("目标高度相对玩家 Mesh 的抬高（米）；略小可拉近垂直方向，减轻玩家躲在机腹下打不中")]
    [Min(-2f)]
    public float heightAbovePlayerMeters = 8f;
    [Min(0.05f)] public float positionSmoothTime = 0.42f;
    [Min(0.1f)] public float maxMoveSpeed = 28f;

    [Header("漂浮")]
    public float bobVerticalAmplitude = 0.22f;
    public float bobVerticalFrequencyHz = 0.38f;

    [Header("朝向")]
    [Tooltip("与玩家 Mesh 距离小于等于此值时，视为接战：水平朝向玩家（供射击与朝向共用）")]
    public float engagementDistanceMeters = 52f;
    public float turnLerp = 8f;

    [Header("环境碰撞避让")]
    [Tooltip("撞到非玩家后，在若干秒内启用抬升 + 放宽距离")]
    public float obstacleAvoidBoostSeconds = 3f;
    [Tooltip("避让期间目标高度相对当前机体每秒抬升（米/秒），与 SmoothDamp 叠加")]
    public float obstacleLiftMetersPerSecond = 6f;
    [Tooltip("避让期间额外加在目标高度上的偏置（米）")]
    public float obstacleLiftHeightBias = 4f;

    [Header("穿墙防护")]
    [Tooltip("SphereCast 使用的层；应包含墙体/地面等静态碰撞体")]
    public LayerMask obstacleCastMask = ~0;
    [Tooltip("沿移动方向探测时使用的球半径，略小于机体包围盒半宽")]
    public float obstacleProbeRadius = 2.8f;
    [Tooltip("碰到阻挡后沿移动方向回退的距离，避免贴面抖动")]
    public float obstacleSkinWidth = 0.12f;
    [Tooltip("忽略本物体及子层级上的 Collider，避免打到自身网格")]
    public bool skipCollidersOnSelfHierarchy = true;

    [Header("受阻脱困")]
    [Tooltip("理想环绕点与 SphereCast 截断后位移差超过此值（米）时，视为贴障，触发侧向找空")]
    public float stuckDistanceThreshold = 2.5f;
    [Tooltip("侧向脱困偏置的最大水平速度（米/秒），随时间衰减")]
    public float evadeHorizontalSpeed = 14f;
    [Tooltip("水平射线探测最远距离（米）")]
    public float evadeProbeDistance = 36f;
    [Tooltip("脱困偏置衰减速率")]
    public float evadeDecay = 2.2f;

    /// <summary>本帧是否处于接战距离内（供 <see cref="PlaneCombat"/> 使用）。</summary>
    public bool PlayerInEngagementRange { get; private set; }

    Transform _playerMesh;
    MechController _playerMech;
    Rigidbody _rb;
    Vector3 _smoothVel;
    float _bobPhase;
    float _avoidEnvUntil;
    Vector3 _evadeHorizontalVel;

    void Awake()
    {
        _bobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        if (front == null)
            front = FindChildDepthFirst(transform, "front");

        if (obstacleProbeRadius < 0.15f)
        {
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Vector3 half = Vector3.Scale(box.size * 0.5f, transform.lossyScale);
                obstacleProbeRadius = Mathf.Clamp(Mathf.Max(half.x, half.y, half.z) * 0.45f, 1.2f, 8f);
            }
            else
                obstacleProbeRadius = 2.5f;
        }

        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.None;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    void FixedUpdate()
    {
        if (_rb != null && _rb.isKinematic)
            _rb.MovePosition(transform.position);
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
        float holdHoriz = avoidLift ? preferredHorizontalDistanceExpandedMeters : preferredHorizontalDistanceMeters;

        Vector3 flatDelta = e - p;
        flatDelta.y = 0f;
        float flatLen = flatDelta.magnitude;
        Vector3 horizDir = flatLen > 0.02f
            ? flatDelta / flatLen
            : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

        Vector3 ring = p + horizDir * holdHoriz;
        ring.y = p.y + heightAbovePlayerMeters;
        float bob = Mathf.Sin((Time.time * bobVerticalFrequencyHz * (Mathf.PI * 2f)) + _bobPhase) * bobVerticalAmplitude;
        ring.y += bob;
        if (avoidLift)
        {
            ring.y = Mathf.Max(ring.y, e.y + obstacleLiftMetersPerSecond * Time.deltaTime);
            ring.y = Mathf.Max(ring.y, p.y + obstacleLiftHeightBias);
        }

        Vector3 ringBeforeClamp = ring;
        ring = ClampDestinationAgainstStaticGeometry(e, ring);
        if (Vector3.Distance(ringBeforeClamp, ring) > stuckDistanceThreshold)
        {
            _avoidEnvUntil = Time.time + Mathf.Max(0.2f, obstacleAvoidBoostSeconds);
            Vector3 open = PickOpenHorizontalDirection(e);
            _evadeHorizontalVel = open * evadeHorizontalSpeed;
        }

        float dt = Time.deltaTime;
        _evadeHorizontalVel = Vector3.Lerp(_evadeHorizontalVel, Vector3.zero, 1f - Mathf.Exp(-evadeDecay * dt));
        Vector3 evade = _evadeHorizontalVel * dt;
        evade.y = 0f;
        ring += evade;
        ring = ClampDestinationAgainstStaticGeometry(e, ring);

        transform.position = Vector3.SmoothDamp(transform.position, ring, ref _smoothVel, positionSmoothTime, maxMoveSpeed,
            dt);

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
        Vector3 open = PickOpenHorizontalDirection(transform.position);
        _evadeHorizontalVel = open * evadeHorizontalSpeed * 0.65f;
    }

    /// <summary>在水平面上选一条 SphereCast 最远的方向，用于暂时离开障碍再回环绕轨道。</summary>
    Vector3 PickOpenHorizontalDirection(Vector3 from)
    {
        float best = -1f;
        Vector3 bestDir = Vector3.zero;
        const int rays = 12;
        float probe = Mathf.Max(4f, evadeProbeDistance);
        float r = Mathf.Max(0.15f, obstacleProbeRadius * 0.55f);
        for (int i = 0; i < rays; i++)
        {
            float ang = i * (Mathf.PI * 2f / rays);
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            var hits = Physics.SphereCastAll(from + Vector3.up * 0.5f, r, dir, probe, obstacleCastMask,
                QueryTriggerInteraction.Ignore);
            float clear = probe;
            if (hits != null && hits.Length > 0)
            {
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    if (h.collider == null)
                        continue;
                    if (skipCollidersOnSelfHierarchy && h.collider.transform.IsChildOf(transform))
                        continue;
                    if (IsPlayerHierarchy(h.collider.gameObject))
                        continue;
                    clear = Mathf.Min(clear, Mathf.Max(0f, h.distance - obstacleSkinWidth));
                    break;
                }
            }

            if (clear > best)
            {
                best = clear;
                bestDir = dir;
            }
        }

        return bestDir.sqrMagnitude > 0.0001f ? bestDir.normalized : FlatForwardOrFallback();
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

    /// <summary>将理想目标点沿「当前→目标」路径截断在首个静态阻挡之前，避免运动学直接位移穿模。</summary>
    Vector3 ClampDestinationAgainstStaticGeometry(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float len = delta.magnitude;
        if (len < 1e-5f)
            return to;
        Vector3 dir = delta / len;
        var hits = Physics.SphereCastAll(from, obstacleProbeRadius, dir, len, obstacleCastMask,
            QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return to;
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        float maxAllowed = len;
        foreach (var h in hits)
        {
            if (h.collider == null)
                continue;
            if (skipCollidersOnSelfHierarchy && h.collider.transform.IsChildOf(transform))
                continue;
            if (IsPlayerHierarchy(h.collider.gameObject))
                continue;
            maxAllowed = Mathf.Min(maxAllowed, Mathf.Max(0f, h.distance - obstacleSkinWidth));
            break;
        }

        return from + dir * maxAllowed;
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
