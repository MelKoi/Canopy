using System.Collections;
using UnityEngine;

/// <summary>
/// 射线射击：鼠标左键、右键与 Q、E 对应四槽（右手 / 左手 / 左肩 / 右肩）。
/// 发射点优先为该槽 Hand 或 Shlouder 下当前装备子物体上的「炮口」「枪口」；若无装备或未命名则回退到序列化的备用 Transform。
/// 弹药：弹匣内可立即射击数 + 备弹；换弹完成后从备弹装入「每次装填数」，不超过弹匣上限。各武器数值按装备根物体名称识别。
/// 火箭筒：先停步再发射，弹大、慢，对敌伤害 ×2（可配）。
/// 连发枪：按住攻击键连续发射，弹体更小，对敌伤害按系数（默认 30%）结算。
/// </summary>
public class WeaponRaycastShooter : MonoBehaviour
{
    const float AimRayDistance = 2000f;
    const float DirEpsilonSq = 0.0001f;

    [Header("枪口参照")]
    public Transform front;
    [Tooltip("无装备或无炮口/枪口时左手槽（鼠标右键）备用发射点")]
    public Transform leftGun;
    [Tooltip("无装备或无炮口/枪口时右手槽（鼠标左键）备用发射点")]
    public Transform rightGun;
    [Tooltip("无装备或无炮口/枪口时左肩槽（Q）备用发射点")]
    public Transform leftShoulder;
    [Tooltip("无装备或无炮口/枪口时右肩槽（E）备用发射点")]
    public Transform rightShoulder;

    [Tooltip("机甲 Mesh 根（含 LeftArm/RightArm）；不填则自动在子层级查找")]
    public Transform mechMeshRoot;

    [Tooltip("若赋值则覆盖该槽：始终从该 Transform 开火（调试用）")]
    public Transform overrideFireLMB;
    public Transform overrideFireRMB;
    public Transform overrideFireQ;
    public Transform overrideFireE;

    [Header("瞄准")]
    [Tooltip("屏幕中心射线来源，一般为 Main Camera")]
    public Camera aimCamera;
    [Tooltip("锁定系统；有当前目标时瞄准方向偏向该目标")]
    public LockOnSystem lockOnSystem;

    [Header("弹道")]
    [Range(300f, 500f)]
    public float bulletSpeed = 420f;
    public float bulletDiameter = 0.11f;
    public float spawnForward = 0.35f;
    public float maxBulletRange = 680f;

    [Header("对敌伤害（EnemyHitFeedback）")]
    [Tooltip("单发枪 / 单发炮 / 未分类武器单发基准伤害；火箭筒、连发枪在此基础上乘各自系数")]
    public float baseEnemyDamagePerBullet = 1f;

    public LayerMask bulletHitMask = ~0;

    [Header("屏幕")]
    public CameraController cameraShake;

    [Header("换弹")]
    public float reloadDuration = 2f;
    public KeyCode reloadKey = KeyCode.R;
    [Tooltip("按下 R 后，在此时间内再按对应武器开火键才会换该槽弹匣")]
    public float reloadComboWindow = 1.2f;

    [Header("弹药 — 火箭筒（名称含「火箭筒」）")]
    [Tooltip("弹匣容量（可立即发射发数）")]
    [SerializeField] int rocketMagazineMax = 1;
    [Tooltip("每次换弹完成时装入弹匣的发数")]
    [SerializeField] int rocketReloadRoundsPerAction = 1;
    [Tooltip("备弹上限")]
    [SerializeField] int rocketReserveMax = 30;

    [Header("弹药 — 连发枪（名称含「连发枪」）")]
    [SerializeField] int burstMagazineMax = 30;
    [SerializeField] int burstReloadRoundsPerAction = 30;
    [SerializeField] int burstReserveMax = 180;

    [Header("弹药 — 单发枪（名称含「单发枪」）")]
    [SerializeField] int singleGunMagazineMax = 10;
    [SerializeField] int singleGunReloadRoundsPerAction = 10;
    [SerializeField] int singleGunReserveMax = 100;

    [Header("弹药 — 单发炮（名称含「单发炮」）")]
    [SerializeField] int singleCannonMagazineMax = 10;
    [SerializeField] int singleCannonReloadRoundsPerAction = 10;
    [SerializeField] int singleCannonReserveMax = 100;

    [Header("弹药 — 未识别武器")]
    [SerializeField] int defaultMagazineMax = 10;
    [SerializeField] int defaultReloadRoundsPerAction = 10;
    [SerializeField] int defaultReserveMax = 100;

    [Header("火箭筒（名称含「火箭筒」的装备）")]
    [SerializeField] float rocketBulletDiameterMultiplier = 2.2f;
    [SerializeField, Range(0.15f, 1f)] float rocketBulletSpeedMultiplier = 0.48f;
    [Tooltip("火箭筒对敌伤害 = 基准 × 该整数（至少为 1）")]
    [SerializeField] int rocketEnemyHitMultiplier = 2;
    [SerializeField] float rocketMinShotInterval = 0.95f;
    [SerializeField] float rocketStopBeforeFireSeconds = 0.78f;
    [SerializeField] float rocketPostFireMovementLockHold = 0.12f;

    [Header("连发枪（名称含「连发枪」的装备）")]
    [SerializeField] float burstFireInterval = 0.1f;
    [SerializeField, Range(0.2f, 1f)] float burstBulletDiameterMultiplier = 0.52f;
    [SerializeField, Range(0.05f, 1f)]
    [Tooltip("连发枪对敌伤害 = 基准 × 该系数")]
    float burstEnemyHitContribution = 0.3f;

    Transform _resolvedMeshRoot;
    Collider[] _selfColliders;
    int[] _magazineRounds;
    int[] _reserveRounds;
    int[] _trackedWeaponInstanceId;
    float[] _reloadEndTime;
    float _reloadComboUntil = -1f;
    float[] _nextRocketFireAllowed = { -999f, -999f, -999f, -999f };
    float[] _nextBurstFireAllowed = { -999f, -999f, -999f, -999f };
    bool _rocketFireRoutineRunning;

    struct AmmoProfile
    {
        public int MagazineMax;
        public int ReloadPerAction;
        public int ReserveMax;
    }

    void Awake()
    {
        _resolvedMeshRoot = mechMeshRoot != null ? mechMeshRoot : DiscoverMechMeshRoot();

        _selfColliders = GetComponentsInChildren<Collider>(true);
        if (aimCamera == null)
            aimCamera = Camera.main;
        cameraShake ??= aimCamera != null ? aimCamera.GetComponent<CameraController>() : null;
        cameraShake ??= FindFirstObjectByType<CameraController>();
        if (lockOnSystem == null)
            lockOnSystem = FindFirstObjectByType<LockOnSystem>();

        _magazineRounds = new int[4];
        _reserveRounds = new int[4];
        _trackedWeaponInstanceId = new int[4];
        for (int i = 0; i < 4; i++)
            _trackedWeaponInstanceId[i] = int.MinValue;
        _reloadEndTime = new float[4];
    }

    AmmoProfile GetAmmoProfileForWeapon(Transform weaponRoot)
    {
        if (MechWeaponMuzzleResolver.IsRocketLauncherWeapon(weaponRoot))
            return new AmmoProfile
            {
                MagazineMax = Mathf.Max(1, rocketMagazineMax),
                ReloadPerAction = Mathf.Max(1, rocketReloadRoundsPerAction),
                ReserveMax = Mathf.Max(0, rocketReserveMax)
            };
        if (MechWeaponMuzzleResolver.IsBurstRifleWeapon(weaponRoot))
            return new AmmoProfile
            {
                MagazineMax = Mathf.Max(1, burstMagazineMax),
                ReloadPerAction = Mathf.Max(1, burstReloadRoundsPerAction),
                ReserveMax = Mathf.Max(0, burstReserveMax)
            };
        if (MechWeaponMuzzleResolver.IsSingleShotCannonWeapon(weaponRoot))
            return new AmmoProfile
            {
                MagazineMax = Mathf.Max(1, singleCannonMagazineMax),
                ReloadPerAction = Mathf.Max(1, singleCannonReloadRoundsPerAction),
                ReserveMax = Mathf.Max(0, singleCannonReserveMax)
            };
        if (MechWeaponMuzzleResolver.IsSingleShotGunWeapon(weaponRoot))
            return new AmmoProfile
            {
                MagazineMax = Mathf.Max(1, singleGunMagazineMax),
                ReloadPerAction = Mathf.Max(1, singleGunReloadRoundsPerAction),
                ReserveMax = Mathf.Max(0, singleGunReserveMax)
            };
        return new AmmoProfile
        {
            MagazineMax = Mathf.Max(1, defaultMagazineMax),
            ReloadPerAction = Mathf.Max(1, defaultReloadRoundsPerAction),
            ReserveMax = Mathf.Max(0, defaultReserveMax)
        };
    }

    void SyncSlotWeaponIdentityAndAmmo(int slot)
    {
        if (!TryGetMuzzleAndWeaponRoot(slot, out _, out Transform weaponRoot))
            return;
        int wid = weaponRoot != null ? weaponRoot.GetInstanceID() : 0;
        if (_trackedWeaponInstanceId[slot] == wid)
            return;
        _trackedWeaponInstanceId[slot] = wid;
        var p = GetAmmoProfileForWeapon(weaponRoot);
        _magazineRounds[slot] = p.MagazineMax;
        _reserveRounds[slot] = p.ReserveMax;
    }

    void ApplyReloadCompletion(int slot)
    {
        if (!TryGetMuzzleAndWeaponRoot(slot, out _, out Transform weaponRoot))
            return;
        var p = GetAmmoProfileForWeapon(weaponRoot);
        int space = p.MagazineMax - _magazineRounds[slot];
        if (space <= 0 || _reserveRounds[slot] <= 0)
            return;
        int take = Mathf.Min(p.ReloadPerAction, space, _reserveRounds[slot]);
        _magazineRounds[slot] += take;
        _reserveRounds[slot] -= take;
    }

    void Update()
    {
        for (int i = 0; i < 4; i++)
            SyncSlotWeaponIdentityAndAmmo(i);

        float t = Time.time;
        for (int i = 0; i < 4; i++)
        {
            if (_reloadEndTime[i] > 0f && t >= _reloadEndTime[i])
            {
                ApplyReloadCompletion(i);
                _reloadEndTime[i] = 0f;
            }
        }

        if (Input.GetKeyDown(reloadKey))
            _reloadComboUntil = Time.time + reloadComboWindow;
        if (_reloadComboUntil > 0f && Time.time > _reloadComboUntil)
            _reloadComboUntil = -1f;

        bool comboActive = _reloadComboUntil > Time.time;
        bool reloadUsed = false;
        if (comboActive)
        {
            if (Input.GetMouseButtonDown(0) && TryReloadSlotOnly(0)) reloadUsed = true;
            else if (Input.GetMouseButtonDown(1) && TryReloadSlotOnly(1)) reloadUsed = true;
            else if (Input.GetKeyDown(KeyCode.Q) && TryReloadSlotOnly(2)) reloadUsed = true;
            else if (Input.GetKeyDown(KeyCode.E) && TryReloadSlotOnly(3)) reloadUsed = true;
            if (reloadUsed)
                _reloadComboUntil = -1f;
        }

        if (!reloadUsed)
            ProcessWeaponFireInputs();
    }

    void ProcessWeaponFireInputs()
    {
        for (int slot = 0; slot < 4; slot++)
        {
            if (!TryGetMuzzleAndWeaponRoot(slot, out Transform muzzle, out Transform weaponRoot))
                continue;

            if (MechWeaponMuzzleResolver.IsRocketLauncherWeapon(weaponRoot))
            {
                if (SlotFirePressedDown(slot))
                    TryStartRocketFire(slot, muzzle);
                continue;
            }

            if (MechWeaponMuzzleResolver.IsBurstRifleWeapon(weaponRoot))
            {
                if (SlotFireHeld(slot) && Time.time >= _nextBurstFireAllowed[slot])
                {
                    if (TryConsumeAmmo(slot))
                    {
                        FireFrom(muzzle, false, true);
                        _nextBurstFireAllowed[slot] = Time.time + Mathf.Max(0.02f, burstFireInterval);
                    }
                }

                continue;
            }

            if (SlotFirePressedDown(slot))
            {
                if (TryConsumeAmmo(slot))
                    FireFrom(muzzle, false, false);
            }
        }
    }

    static bool SlotFirePressedDown(int slot)
    {
        return slot switch
        {
            0 => Input.GetMouseButtonDown(0),
            1 => Input.GetMouseButtonDown(1),
            2 => Input.GetKeyDown(KeyCode.Q),
            3 => Input.GetKeyDown(KeyCode.E),
            _ => false
        };
    }

    static bool SlotFireHeld(int slot)
    {
        return slot switch
        {
            0 => Input.GetMouseButton(0),
            1 => Input.GetMouseButton(1),
            2 => Input.GetKey(KeyCode.Q),
            3 => Input.GetKey(KeyCode.E),
            _ => false
        };
    }

    bool TryGetMuzzleAndWeaponRoot(int slot, out Transform muzzle, out Transform weaponRoot)
    {
        muzzle = GetMuzzleForRaySlot(slot);
        weaponRoot = null;
        if (muzzle == null)
            return false;
        var mount = MechWeaponMuzzleResolver.ResolveMountForWeaponRaySlot(_resolvedMeshRoot, slot);
        weaponRoot = MechWeaponMuzzleResolver.GetWeaponRootUnderMountForDescendant(mount, muzzle);
        return true;
    }

    void TryStartRocketFire(int slotIndex, Transform muzzle)
    {
        if (Time.time < _nextRocketFireAllowed[slotIndex])
            return;
        if (_rocketFireRoutineRunning)
            return;
        if (!TryConsumeAmmo(slotIndex))
            return;
        _nextRocketFireAllowed[slotIndex] = Time.time + Mathf.Max(0.05f, rocketMinShotInterval);
        StartCoroutine(FireRocketLauncherRoutine(muzzle));
    }

    Transform DiscoverMechMeshRoot()
    {
        var byName = transform.Find("Mesh");
        if (byName != null && MechWeaponMuzzleResolver.MeshRootHasLeftArm(byName))
            return byName;
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (MechWeaponMuzzleResolver.MeshRootHasLeftArm(c))
                return c;
        }

        if (front != null)
        {
            for (Transform t = front; t != null; t = t.parent)
            {
                if (MechWeaponMuzzleResolver.MeshRootHasLeftArm(t))
                    return t;
            }
        }

        return null;
    }

    Transform GetMuzzleForRaySlot(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0:
                if (overrideFireLMB != null)
                    return overrideFireLMB;
                break;
            case 1:
                if (overrideFireRMB != null)
                    return overrideFireRMB;
                break;
            case 2:
                if (overrideFireQ != null)
                    return overrideFireQ;
                break;
            case 3:
                if (overrideFireE != null)
                    return overrideFireE;
                break;
        }

        var mount = MechWeaponMuzzleResolver.ResolveMountForWeaponRaySlot(_resolvedMeshRoot, slotIndex);
        var muzzle = MechWeaponMuzzleResolver.FindMuzzleOnMount(mount);
        if (muzzle != null)
            return muzzle;

        switch (slotIndex)
        {
            case 0:
                return rightGun;
            case 1:
                return leftGun;
            case 2:
                return leftShoulder;
            case 3:
                return rightShoulder;
            default:
                return null;
        }
    }

    /// <summary>弹匣内当前可射击发数。</summary>
    public int GetMagazineAmmo(int slot)
    {
        if (_magazineRounds == null || slot < 0 || slot >= _magazineRounds.Length)
            return 0;
        return _magazineRounds[slot];
    }

    /// <summary>备弹剩余。</summary>
    public int GetReserveAmmo(int slot)
    {
        if (_reserveRounds == null || slot < 0 || slot >= _reserveRounds.Length)
            return 0;
        return _reserveRounds[slot];
    }

    public bool IsReloadingSlot(int slot)
    {
        if (_reloadEndTime == null || slot < 0 || slot >= _reloadEndTime.Length)
            return false;
        return _reloadEndTime[slot] > Time.time;
    }

    bool TryReloadSlotOnly(int slot)
    {
        if (_magazineRounds == null || slot < 0 || slot >= _magazineRounds.Length)
            return false;
        if (!TryGetMuzzleAndWeaponRoot(slot, out _, out Transform wr))
            return false;
        if (IsReloadingSlot(slot))
            return false;
        var p = GetAmmoProfileForWeapon(wr);
        if (_magazineRounds[slot] >= p.MagazineMax)
            return false;
        if (_reserveRounds[slot] <= 0)
            return false;
        _reloadEndTime[slot] = Time.time + reloadDuration;
        return true;
    }

    bool TryConsumeAmmo(int slot)
    {
        if (_magazineRounds == null)
            return true;
        if (slot < 0 || slot >= _magazineRounds.Length)
            return false;
        if (IsReloadingSlot(slot))
            return false;
        if (_magazineRounds[slot] > 0)
        {
            _magazineRounds[slot]--;
            return true;
        }

        BeginReload(slot);
        return false;
    }

    void BeginReload(int slot)
    {
        if (IsReloadingSlot(slot))
            return;
        if (!TryGetMuzzleAndWeaponRoot(slot, out _, out Transform wr))
            return;
        var p = GetAmmoProfileForWeapon(wr);
        if (_magazineRounds[slot] >= p.MagazineMax)
            return;
        if (_reserveRounds[slot] <= 0)
            return;
        _reloadEndTime[slot] = Time.time + reloadDuration;
    }

    IEnumerator FireRocketLauncherRoutine(Transform muzzle)
    {
        _rocketFireRoutineRunning = true;
        MechController mech = GetComponentInChildren<MechController>(true);
        mech?.PushWeaponMovementLock();
        try
        {
            yield return new WaitForFixedUpdate();

            float stop = Mathf.Max(0f, rocketStopBeforeFireSeconds);
            if (stop > 0f)
                yield return new WaitForSeconds(stop);

            if (muzzle != null)
                FireFrom(muzzle, true, false);

            if (rocketPostFireMovementLockHold > 0f)
                yield return new WaitForSeconds(rocketPostFireMovementLockHold);
        }
        finally
        {
            mech?.PopWeaponMovementLock();
            _rocketFireRoutineRunning = false;
        }
    }

    void FireFrom(Transform muzzle, bool rocketLauncherShot, bool burstRifleShot)
    {
        Vector3 dir = GetAimDirection(muzzle.position);
        float speed = bulletSpeed;
        float diameter = bulletDiameter;
        float damage = baseEnemyDamagePerBullet;

        if (rocketLauncherShot)
        {
            speed *= rocketBulletSpeedMultiplier;
            diameter *= rocketBulletDiameterMultiplier;
            damage *= Mathf.Max(1f, rocketEnemyHitMultiplier);
        }
        else if (burstRifleShot)
        {
            diameter *= burstBulletDiameterMultiplier;
            damage *= Mathf.Clamp(burstEnemyHitContribution, 0.05f, 1f);
        }

        float life = maxBulletRange / Mathf.Max(speed, 1f) + 0.5f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        if (rocketLauncherShot)
            go.name = "BulletRocket";
        else if (burstRifleShot)
            go.name = "BulletBurst";
        else
            go.name = "Bullet";
        go.layer = gameObject.layer;
        go.transform.position = muzzle.position + dir * spawnForward;
        go.transform.localScale = Vector3.one * diameter;

        var rend = go.GetComponent<Renderer>();
        if (rocketLauncherShot)
            ProjectileBullet.ApplyBrightBody(rend, new Color(1f, 0.45f, 0.12f), new Color(1.6f, 0.5f, 0.1f));
        else if (burstRifleShot)
            ProjectileBullet.ApplyBrightBody(rend, new Color(0.2f, 0.75f, 0.95f), new Color(0.35f, 1f, 1.15f));
        else
            ProjectileBullet.ApplyBrightBody(rend, new Color(0.25f, 0.85f, 1f), new Color(0.4f, 1.2f, 1.4f));

        var proj = go.AddComponent<ProjectileBullet>();
        Transform homingTarget = lockOnSystem != null ? lockOnSystem.currentTarget : null;
        proj.Setup(speed, dir, _selfColliders, life, homingTarget, enemyDamage: damage);

        if (rocketLauncherShot)
            cameraShake?.AddShootScreenShake(1.35f);
        else if (burstRifleShot)
            cameraShake?.AddShootScreenShake(0.35f);
        else
            cameraShake?.AddShootScreenShake(1f);
    }

    Vector3 GetAimDirection(Vector3 fromWorld)
    {
        if (lockOnSystem != null && lockOnSystem.currentTarget != null)
        {
            Vector3 d = lockOnSystem.currentTarget.position - fromWorld;
            if (d.sqrMagnitude > DirEpsilonSq)
                return d.normalized;
        }

        if (aimCamera != null)
        {
            Ray r = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 hit = r.origin + r.direction * AimRayDistance;
            if (Physics.Raycast(r, out RaycastHit rh, AimRayDistance, bulletHitMask, QueryTriggerInteraction.Ignore))
                hit = rh.point;
            Vector3 d = hit - fromWorld;
            if (d.sqrMagnitude > DirEpsilonSq)
                return d.normalized;
            return r.direction.normalized;
        }

        if (front != null)
            return front.forward;
        return transform.forward;
    }
}
