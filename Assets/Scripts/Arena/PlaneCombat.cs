using UnityEngine;

/// <summary>
/// Plane Boss：<c>FrontGun</c> 下 GUN2/3 发射与小兵同款的敌人子弹（更高射速与弹速，每管 120 发后冷却）；
/// GUN1/4 使用与玩家 <see cref="WeaponRaycastShooter"/> 相同的弹体参数持续射击。
/// </summary>
[DefaultExecutionOrder(20)]
public class PlaneCombat : MonoBehaviour
{
    const float MinionDefaultDetectionRadius = 5f;

    [Header("接战")]
    [Tooltip("若未挂 PlaneDistanceHoverAI，则用此项判断与玩家 Mesh 的距离以开火")]
    public float engageFallbackDistance = 36f;

    [Header("GUN2 / GUN3 小兵弹")]
    [Tooltip("单管 120 发，两管同时开火；约 60 秒内打完整夹 = 每 0.5 秒齐射一轮")]
    public int minionBurstShotsPerGun = 120;
    public float minionBurstDurationSeconds = 60f;
    [Tooltip("相对默认小兵弹速 (35) 的倍率，最终取 max(35, 35*倍率)")]
    public float minionBulletSpeedMultiplier = 2.4f;
    public float bulletDiameterEnemy = 0.24f;
    public float spawnForwardEnemy = 0.6f;
    public int projectileHealthDamage = -1;
    public int projectileToughnessDelta = -1;
    public float minionBurstCooldownSeconds = 15f;

    [Header("GUN1 / GUN4 玩家弹")]
    [Tooltip("两管齐射间隔（秒）")]
    public float playerMirrorFireInterval = 0.1f;

    Transform _frontGun;
    Transform _gun1;
    Transform _gun2;
    Transform _gun3;
    Transform _gun4;
    Collider[] _selfColliders;
    Transform _playerMesh;
    WeaponRaycastShooter _playerWeapon;
    PlaneDistanceHoverAI _hover;

    int _minionVolleysFired;
    bool _minionBurstInCooldown;
    float _minionCooldownEndTime;
    float _nextMinionVolleyTime = -1f;
    float _nextPlayerStyleTime = -1f;

    void Awake()
    {
        _selfColliders = GetComponentsInChildren<Collider>(true);
        _hover = GetComponent<PlaneDistanceHoverAI>();
        ResolveGuns();
    }

    void ResolveGuns()
    {
        _frontGun = FindChildDepthFirst(transform, "FrontGun");
        if (_frontGun == null)
        {
            Debug.LogWarning("PlaneCombat: 未找到 FrontGun。");
            return;
        }

        _gun1 = FindChildDepthFirst(_frontGun, "GUN1");
        _gun2 = FindChildDepthFirst(_frontGun, "GUN2");
        _gun3 = FindChildDepthFirst(_frontGun, "GUN3");
        _gun4 = FindChildDepthFirst(_frontGun, "GUN4");
    }

    void LateUpdate()
    {
        if (!EnsurePlayerAndWeapon())
            return;

        if (!IsEngaged())
            return;

        float t = Time.time;
        TickPlayerStyleGuns(t);
        TickMinionGuns(t);
    }

    bool IsEngaged()
    {
        if (_hover != null)
            return _hover.PlayerInEngagementRange;
        if (_playerMesh == null)
            return false;
        return Vector3.Distance(_playerMesh.position, transform.position) <= engageFallbackDistance;
    }

    void TickPlayerStyleGuns(float t)
    {
        if (_gun1 == null || _gun4 == null || _playerWeapon == null)
            return;

        if (_nextPlayerStyleTime >= 0f && t < _nextPlayerStyleTime)
            return;
        _nextPlayerStyleTime = t + Mathf.Max(0.02f, playerMirrorFireInterval);

        FirePlayerMirrorBullet(_gun1);
        FirePlayerMirrorBullet(_gun4);
    }

    void TickMinionGuns(float t)
    {
        if (_gun2 == null || _gun3 == null)
            return;

        if (_minionBurstInCooldown)
        {
            if (t >= _minionCooldownEndTime)
            {
                _minionBurstInCooldown = false;
                _minionVolleysFired = 0;
                _nextMinionVolleyTime = t;
            }
            else
                return;
        }

        if (_minionVolleysFired >= minionBurstShotsPerGun)
        {
            _minionBurstInCooldown = true;
            _minionCooldownEndTime = t + Mathf.Max(0.1f, minionBurstCooldownSeconds);
            return;
        }

        float volleyInterval = Mathf.Max(0.04f, minionBurstDurationSeconds / Mathf.Max(1, minionBurstShotsPerGun));
        if (t < _nextMinionVolleyTime)
            return;

        _nextMinionVolleyTime = t + volleyInterval;
        float enemySpeed = Mathf.Max(35f, 35f * minionBulletSpeedMultiplier);
        FireEnemyStyleBullet(_gun2, enemySpeed);
        FireEnemyStyleBullet(_gun3, enemySpeed);
        _minionVolleysFired++;
    }

    void FireEnemyStyleBullet(Transform muzzle, float speed)
    {
        Vector3 dir = AimDirToPlayer(muzzle.position);
        Vector3 pos = muzzle.position + dir * spawnForwardEnemy;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EnemyBullet";
        go.layer = gameObject.layer;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * bulletDiameterEnemy;
        var rend = go.GetComponent<Renderer>();
        var body = new Color(1f, 0.42f, 0.1f);
        var emit = new Color(1.8f, 0.55f, 0.12f);
        ProjectileBullet.ApplyBrightBody(rend, body, emit);

        var trail = go.AddComponent<TrailRenderer>();
        ProjectileBullet.ConfigureReadableTrail(trail, bulletDiameterEnemy,
            new Color(1f, 0.65f, 0.2f, 1f),
            new Color(1f, 0.25f, 0f, 0.08f));

        float life = Mathf.Max(8f, MinionDefaultDetectionRadius * 2f / Mathf.Max(speed, 1f));
        var proj = go.AddComponent<EnemyProjectileBullet>();
        Collider[] ignore = _selfColliders != null && _selfColliders.Length > 0 ? _selfColliders : null;
        proj.Setup(speed, dir, ignore, life, projectileHealthDamage, projectileToughnessDelta);
    }

    void FirePlayerMirrorBullet(Transform muzzle)
    {
        var w = _playerWeapon;
        if (w == null || _playerMesh == null)
            return;

        Vector3 dir = AimDirToPlayer(muzzle.position);
        float life = w.maxBulletRange / Mathf.Max(w.bulletSpeed, 1f) + 0.5f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Bullet";
        go.layer = gameObject.layer;
        go.transform.position = muzzle.position + dir * w.spawnForward;
        go.transform.localScale = Vector3.one * w.bulletDiameter;

        var rend = go.GetComponent<Renderer>();
        ProjectileBullet.ApplyBrightBody(rend, new Color(0.25f, 0.85f, 1f), new Color(0.4f, 1.2f, 1.4f));

        var proj = go.AddComponent<ProjectileBullet>();
        proj.Setup(w.bulletSpeed, dir, _selfColliders, life, _playerMesh, damagePlayerLikeEnemy: true);
    }

    Vector3 AimDirToPlayer(Vector3 fromWorld)
    {
        Vector3 d = _playerMesh.position - fromWorld;
        return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
    }

    bool EnsurePlayerAndWeapon()
    {
        if (_playerMesh != null && _playerWeapon != null)
            return _gun1 != null && _gun4 != null;

        var mech = FindFirstObjectByType<MechController>();
        if (mech == null)
            return false;

        _playerWeapon ??= mech.GetComponentInChildren<WeaponRaycastShooter>(true);
        if (_playerMesh == null)
        {
            var t = mech.transform.Find("Mesh");
            _playerMesh = t != null ? t : FindChildDepthFirst(mech.transform, "Mesh");
            if (_playerMesh == null)
                _playerMesh = mech.transform;
        }

        return _playerWeapon != null && _gun1 != null && _gun4 != null;
    }

    static Transform FindChildDepthFirst(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindChildDepthFirst(root.GetChild(i), name);
            if (hit != null)
                return hit;
        }

        return null;
    }
}
