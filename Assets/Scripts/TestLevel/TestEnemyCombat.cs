using System;
using UnityEngine;

/// <summary>
/// 测试敌人：5m 内、无遮挡时每隔若干秒向玩家发射可见子弹；可选原地朝向玩家后再开火。
/// </summary>
public class TestEnemyCombat : MonoBehaviour, IEnemyPatrolSuspendCondition
{
    [HideInInspector] public int spawnPointIndex;

    [Tooltip("为 false 时不会在 3 号刷新点击败后上报测试关卡进度")]
    public bool reportBossFromPoint3 = true;

    [Header("感知 / 开火")]
    public float detectionRadius = 5f;
    [Tooltip("为 false 时只要距离内即视为可交战（射线易被场景几何挡住）")]
    public bool requireLineOfSight = true;
    public float fireInterval = 0.55f;
    public float bulletSpeed = 90f;
    public float bulletDiameter = 0.24f;
    public float spawnForward = 0.6f;
    public float aimHeightOffset = 1f;
    public LayerMask lineOfSightMask = ~0;

    [Header("静止教学 / 朝向")]
    [Tooltip("关闭巡逻组件，不位移")]
    public bool stationaryNoPatrol;
    [Tooltip("水平旋转本体使 enemyfront 指向玩家")]
    public bool facePlayerWhenEngaged;
    [Tooltip("仅当正面已对准玩家时才允许开火")]
    public bool requireFacingToFire;
    public float faceTurnSpeedDeg = 360f;
    public float fireFacingMaxAngleDeg = 15f;

    [Header("子弹对玩家（<0 表示用 PlayerMechResources 默认值）")]
    public int projectileHealthDamage = 100;
    public int projectileToughnessDelta = -1;

    MechController _playerMech;
    Collider[] _selfColliders;
    float _nextFireTime;
    EnemyHitFeedback _feedback;
    Transform _enemyFront;

    void Awake()
    {
        _selfColliders = GetComponentsInChildren<Collider>(true);
        _feedback = GetComponent<EnemyHitFeedback>();
        if (stationaryNoPatrol)
        {
            var patrol = GetComponent<EnemyPatrolAgent>();
            if (patrol != null)
                patrol.enabled = false;
        }

        _enemyFront = null;
        var agent = GetComponent<EnemyPatrolAgent>();
        if (agent != null && agent.enemyFront != null)
            _enemyFront = agent.enemyFront;
        if (_enemyFront == null)
        {
            var t = transform.Find("enemyfront") ?? transform.Find("Enemyfront");
            _enemyFront = t;
        }
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
        if (reportBossFromPoint3 && spawnPointIndex == 3)
            TestLevelProgress.MarkBossFromPoint3Defeated();
    }

    void Update()
    {
        EnsurePlayerCached();
        if (_playerMech == null)
            return;

        if (!TryBuildEngagement(out Vector3 dirToPlayer))
            return;

        if (facePlayerWhenEngaged)
            FacePlayerHorizontally(dirToPlayer, Time.deltaTime);

        if (requireFacingToFire && !IsFacingPlayerWithinAngle(dirToPlayer))
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
        if (_enemyFront != null)
            from = _enemyFront.position + Vector3.up * Mathf.Max(0.08f, aimHeightOffset * 0.2f);
        Vector3 to = aim.position + Vector3.up * (aimHeightOffset * 0.5f);
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist > detectionRadius || dist < 0.01f)
            return false;

        if (requireLineOfSight && !HasClearLineOfSight(from, delta.normalized, dist, aim.root))
            return false;

        dirToPlayerNormalized = delta.normalized;
        return true;
    }

    bool HasClearLineOfSight(Vector3 origin, Vector3 dir, float maxDist, Transform playerRoot)
    {
        dir = dir.normalized;
        var hits = Physics.RaycastAll(origin, dir, maxDist, lineOfSightMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return true;
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            Transform t = h.transform;
            if (t == transform || t.IsChildOf(transform))
                continue;
            return t == playerRoot || t.IsChildOf(playerRoot);
        }

        return true;
    }

    void FacePlayerHorizontally(Vector3 dirToPlayerWorld, float dt)
    {
        Vector3 want = dirToPlayerWorld;
        want.y = 0f;
        if (want.sqrMagnitude < 0.0001f)
            return;
        want.Normalize();

        Vector3 face;
        if (_enemyFront != null)
        {
            face = _enemyFront.position - transform.position;
            face.y = 0f;
        }
        else
            face = transform.forward;
        face.y = 0f;

        if (face.sqrMagnitude < 0.0001f)
            return;

        float signed = Vector3.SignedAngle(face.normalized, want, Vector3.up);
        float maxStep = faceTurnSpeedDeg * dt;
        transform.Rotate(0f, Mathf.Clamp(signed, -maxStep, maxStep), 0f, Space.World);
    }

    bool IsFacingPlayerWithinAngle(Vector3 dirToPlayerWorld)
    {
        Vector3 want = dirToPlayerWorld;
        want.y = 0f;
        if (want.sqrMagnitude < 0.0001f)
            return true;
        want.Normalize();

        Vector3 face;
        if (_enemyFront != null)
        {
            face = _enemyFront.position - transform.position;
            face.y = 0f;
        }
        else
            face = transform.forward;
        face.y = 0f;

        if (face.sqrMagnitude < 0.0001f)
            return true;

        return Vector3.Angle(face.normalized, want) <= fireFacingMaxAngleDeg;
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
        Collider[] ignore = _selfColliders != null && _selfColliders.Length > 0 ? _selfColliders : null;
        proj.Setup(bulletSpeed, dir, ignore, life, projectileHealthDamage, projectileToughnessDelta);
    }
}
