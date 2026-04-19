using UnityEngine;



/// <summary>

/// Plane Boss：<c>FrontGun</c> 下 GUN1～GUN4；每根 GUN 下若有子物体 <c>Fire</c> 则从 Fire 世界位置发射，否则用 GUN 自身 Transform。

/// 接战逻辑与敌人一致：检测到玩家进入攻击范围后开火，不依赖玩家是否挂载武器或其它组件；仅需要能解析出玩家位置（机甲或 <see cref="PlayerMechResources"/>）。

/// </summary>

[DefaultExecutionOrder(20)]

public class PlaneCombat : MonoBehaviour

{

    const float MinionDefaultDetectionRadius = 5f;



    [Header("接战")]

    [Tooltip("与玩家瞄准点的直线距离 ≤ 此值时可开火；应略大于机体环绕半径以免够不着")]

    public float engagementDistanceMeters = 56f;



    [Header("GUN1 / GUN4 蓝色弹")]

    [Tooltip("GUN1+GUN4 每次齐射计 2 枚蓝色弹；累计达此数量后进入冷却")]

    public int primaryBurstTotalBlueBullets = 240;

    [Tooltip("蓝色弹打满一夹后的停火时间（秒）")]

    public float primaryBurstCooldownSeconds = 15f;

    public float primaryPairFireInterval = 0.1f;

    public float primaryBulletSpeed = 420f;

    public float primaryBulletDiameter = 0.11f;

    public float primarySpawnForward = 0.35f;

    public float primaryMaxRange = 400f;

    [Tooltip("GUN1/4 命中扣血；≥0 为固定值，&lt;0 则用 PlayerMechResources 默认")]

    public int primaryPlayerHealthDamage = 50;

    public int primaryPlayerToughnessDelta = -1;

    [Tooltip("蓝色弹体：按机体水平速度方向的横向摆动幅度（ProjectileBullet 内使用）")]

    public float primaryMovementDriftLateral = 0.14f;

    public float primaryMovementDriftWobbleHz = 2f;



    [Header("GUN2 / GUN3 橙色弹（无弹夹冷却，仅齐射间隔）")]

    [Tooltip("橙色齐射间隔（秒）；两管同时开火，无打满停火")]

    public float orangePairFireInterval = 3f;

    [Tooltip("相对默认小兵弹速 (35) 的倍率，最终取 max(35, 35*倍率)")]

    public float minionBulletSpeedMultiplier = 2.4f;

    public float bulletDiameterEnemy = 0.24f;

    public float spawnForwardEnemy = 0.6f;

    [Tooltip("GUN2/3 命中扣血；≥0 为固定值，&lt;0 则用 PlayerMechResources 默认")]

    public int projectileHealthDamage = 200;

    public int projectileToughnessDelta = -1;



    Transform _frontGun;

    Transform _muzzle1;

    Transform _muzzle2;

    Transform _muzzle3;

    Transform _muzzle4;

    Collider[] _selfColliders;

    Transform _playerAimTarget;



    int _primaryBlueBulletsFiredInBurst;

    bool _primaryBurstInCooldown;

    float _primaryCooldownEndTime;

    float _nextMinionVolleyTime = -1f;

    float _nextPrimaryPairTime = -1f;

    Vector3 _prevPos;



    void Awake()

    {

        _selfColliders = GetComponentsInChildren<Collider>(true);

        _prevPos = transform.position;

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



        _muzzle1 = ResolveGunMuzzle(FindChildDepthFirst(_frontGun, "GUN1"));

        _muzzle2 = ResolveGunMuzzle(FindChildDepthFirst(_frontGun, "GUN2"));

        _muzzle3 = ResolveGunMuzzle(FindChildDepthFirst(_frontGun, "GUN3"));

        _muzzle4 = ResolveGunMuzzle(FindChildDepthFirst(_frontGun, "GUN4"));

    }



    /// <summary>优先使用 GUN 下名为 Fire 的子物体作为枪口；否则使用 GUN 根 Transform。</summary>

    static Transform ResolveGunMuzzle(Transform gun)

    {

        if (gun == null)

            return null;

        var fire = FindChildDepthFirst(gun, "Fire");

        return fire != null ? fire : gun;

    }



    void LateUpdate()

    {

        float dt = Mathf.Max(Time.deltaTime, 1e-5f);

        Vector3 deltaPos = transform.position - _prevPos;

        _prevPos = transform.position;

        deltaPos.y = 0f;

        Vector3 planeMoveHorizontal = deltaPos.sqrMagnitude > 1e-8f

            ? (deltaPos / dt).normalized

            : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;



        if (!EnsurePlayerTarget())

            return;



        if (!IsEngaged())

            return;



        float t = Time.time;

        TickPrimaryPairGuns(t, planeMoveHorizontal);

        TickMinionGuns(t);

    }



    bool IsEngaged()

    {

        if (_playerAimTarget == null)

            return false;

        return Vector3.Distance(_playerAimTarget.position, transform.position) <= engagementDistanceMeters;

    }



    void TickPrimaryPairGuns(float t, Vector3 planeMoveHorizontal)

    {

        if (_muzzle1 == null || _muzzle4 == null)

            return;



        int blueCap = Mathf.Max(2, primaryBurstTotalBlueBullets);



        if (_primaryBurstInCooldown)

        {

            if (t >= _primaryCooldownEndTime)

            {

                _primaryBurstInCooldown = false;

                _primaryBlueBulletsFiredInBurst = 0;

                _nextPrimaryPairTime = t;

            }

            else

                return;

        }



        if (_primaryBlueBulletsFiredInBurst >= blueCap)

        {

            _primaryBurstInCooldown = true;

            _primaryCooldownEndTime = t + Mathf.Max(0.1f, primaryBurstCooldownSeconds);

            return;

        }



        if (_nextPrimaryPairTime >= 0f && t < _nextPrimaryPairTime)

            return;

        _nextPrimaryPairTime = t + Mathf.Max(0.02f, primaryPairFireInterval);



        FirePrimaryDamageBullet(_muzzle1, planeMoveHorizontal);

        FirePrimaryDamageBullet(_muzzle4, planeMoveHorizontal);

        _primaryBlueBulletsFiredInBurst += 2;

    }



    void TickMinionGuns(float t)

    {

        if (_muzzle2 == null || _muzzle3 == null)

            return;



        if (t < _nextMinionVolleyTime)

            return;



        float volleyInterval = Mathf.Max(0.04f, orangePairFireInterval);

        _nextMinionVolleyTime = t + volleyInterval;



        float enemySpeed = Mathf.Max(35f, 35f * minionBulletSpeedMultiplier);

        FireEnemyStyleBullet(_muzzle2, enemySpeed);

        FireEnemyStyleBullet(_muzzle3, enemySpeed);

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



    void FirePrimaryDamageBullet(Transform muzzle, Vector3 planeMoveHorizontal)

    {

        if (_playerAimTarget == null)

            return;



        Vector3 dir = AimDirToPlayer(muzzle.position);

        float life = primaryMaxRange / Mathf.Max(primaryBulletSpeed, 1f) + 0.5f;



        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        go.name = "Bullet";

        go.layer = gameObject.layer;

        go.transform.position = muzzle.position + dir * primarySpawnForward;

        go.transform.localScale = Vector3.one * primaryBulletDiameter;



        var rend = go.GetComponent<Renderer>();

        ProjectileBullet.ApplyBrightBody(rend, new Color(0.25f, 0.85f, 1f), new Color(0.4f, 1.2f, 1.4f));



        var proj = go.AddComponent<ProjectileBullet>();

        proj.Setup(primaryBulletSpeed, dir, _selfColliders, life, null, damagePlayerLikeEnemy: true,

            primaryPlayerHealthDamage, primaryPlayerToughnessDelta,

            planeMoveHorizontal, useMovementDriftForPlayerBullet: true,

            primaryMovementDriftLateral, primaryMovementDriftWobbleHz);

    }



    Vector3 AimDirToPlayer(Vector3 fromWorld)

    {

        Vector3 d = _playerAimTarget.position - fromWorld;

        return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;

    }



    /// <summary>解析玩家用于瞄准的 Transform：优先机甲 Mesh，否则带 PlayerMechResources 的物体。不要求玩家挂载武器。</summary>

    bool EnsurePlayerTarget()

    {

        if (_playerAimTarget != null)

            return true;



        var mech = FindFirstObjectByType<MechController>();

        if (mech != null)

        {

            var t = mech.transform.Find("Mesh");

            _playerAimTarget = t != null ? t : FindChildDepthFirst(mech.transform, "Mesh");

            if (_playerAimTarget == null)

                _playerAimTarget = mech.transform;

            return true;

        }



        var resources = FindFirstObjectByType<PlayerMechResources>();

        if (resources != null)

        {

            _playerAimTarget = resources.transform;

            return true;

        }



        return false;

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


