using UnityEngine;

/// <summary>
/// 测试敌人：5m 内、无遮挡时每隔 2 秒向玩家发射可见子弹。
/// </summary>
public class TestEnemyCombat : MonoBehaviour, IEnemyPatrolSuspendCondition
{
    [HideInInspector] public int spawnPointIndex;

    [Header("感知 / 开火")]
    public float detectionRadius = 5f;
    public float fireInterval = 2f;
    public float bulletSpeed = 35f;
    public float bulletDiameter = 0.24f;
    public float spawnForward = 0.6f;
    public float aimHeightOffset = 1f;
    public LayerMask lineOfSightMask = ~0;

    MechController _playerMech;
    Collider _selfCol;
    float _nextFireTime;
    EnemyHitFeedback _feedback;

    void Awake()
    {
        _selfCol = GetComponent<Collider>();
        _feedback = GetComponent<EnemyHitFeedback>();
    }

    void OnEnable()
    {
        if (_feedback != null)
            _feedback.OnFinalHitCommitted += HandleFinalHitCommitted;
    }

    void OnDisable()
    {
        if (_feedback != null)
            _feedback.OnFinalHitCommitted -= HandleFinalHitCommitted;
    }

    void HandleFinalHitCommitted()
    {
        if (spawnPointIndex == 3)
            TestLevelProgress.MarkBossFromPoint3Defeated();
    }

    void Update()
    {
        EnsurePlayerCached();
        if (_playerMech == null)
            return;

        if (!TryBuildEngagement(out Vector3 dirToPlayer))
            return;

        if (Time.time < _nextFireTime)
            return;

        _nextFireTime = Time.time + fireInterval;
        FireToward(dirToPlayer);
    }

    /// <summary>玩家在感知距离内且与敌人之间存在干净视线时暂停巡逻。</summary>
    public bool IsPatrolSuspendedByPlayer() => ShouldSuspendPatrol();

    public bool ShouldSuspendPatrol()
    {
        EnsurePlayerCached();
        if (_playerMech == null)
            return false;
        return TryBuildEngagement(out _);
    }

    void EnsurePlayerCached()
    {
        if (_playerMech == null)
            _playerMech = FindFirstObjectByType<MechController>();
    }

    bool TryBuildEngagement(out Vector3 dirToPlayerNormalized)
    {
        dirToPlayerNormalized = default;
        if (_playerMech == null)
            return false;

        Transform aim = _playerMech.transform;
        Vector3 from = transform.position + Vector3.up * aimHeightOffset * 0.5f;
        Vector3 to = aim.position + Vector3.up * (aimHeightOffset * 0.5f);
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist > detectionRadius || dist < 0.01f)
            return false;

        if (!HasClearLineOfSight(from, delta.normalized, dist, aim.root))
            return false;

        dirToPlayerNormalized = delta.normalized;
        return true;
    }

    bool HasClearLineOfSight(Vector3 origin, Vector3 dir, float maxDist, Transform playerRoot)
    {
        if (!Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, lineOfSightMask, QueryTriggerInteraction.Ignore))
            return true;

        Transform t = hit.transform;
        return t == playerRoot || t.IsChildOf(playerRoot);
    }

    void FireToward(Vector3 dir)
    {
        Vector3 pos = transform.position + dir * spawnForward;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EnemyBullet";
        go.layer = gameObject.layer;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * bulletDiameter;
        var rend = go.GetComponent<Renderer>();
        var body = new Color(1f, 0.42f, 0.1f);
        var emit = new Color(1.8f, 0.55f, 0.12f);
        ProjectileBullet.ApplyBrightBody(rend, body, emit);

        var trail = go.AddComponent<TrailRenderer>();
        ProjectileBullet.ConfigureReadableTrail(trail, bulletDiameter,
            new Color(1f, 0.65f, 0.2f, 1f),
            new Color(1f, 0.25f, 0f, 0.08f));

        float life = Mathf.Max(8f, detectionRadius * 2f / Mathf.Max(bulletSpeed, 1f));
        var proj = go.AddComponent<EnemyProjectileBullet>();
        Collider[] ignore = _selfCol != null ? new[] { _selfCol } : null;
        proj.Setup(bulletSpeed, dir, ignore, life);
    }
}
