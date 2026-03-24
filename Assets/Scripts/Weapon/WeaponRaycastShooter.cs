using UnityEngine;

/// <summary>
/// 射线射击：鼠标左键、右键与 Q、E 绑定不同发射点；从对应位置开火。锁定目标时由 LockOnSystem 帮助瞄准；屏幕中心射线；可选 CameraController 震动。
/// </summary>
public class WeaponRaycastShooter : MonoBehaviour
{
    const float AimRayDistance = 2000f;
    const float DirEpsilonSq = 0.0001f;

    [Header("枪口参照")]
    public Transform front;
    public Transform leftGun;
    public Transform rightGun;
    public Transform leftShoulder;
    public Transform rightShoulder;

    [Tooltip("若赋值则覆盖该键默认使用的开火 Transform。")]
    public Transform overrideFireLMB;
    public Transform overrideFireRMB;
    public Transform overrideFireQ;
    public Transform overrideFireE;

    [Header("瞄准")]
    [Tooltip("屏幕中心射线来源，一般为 Main Camera。")]
    public Camera aimCamera;
    [Tooltip("锁定系统；有当前目标时瞄准方向偏向该目标。")]
    public LockOnSystem lockOnSystem;

    [Header("弹道")]
    [Range(300f, 500f)]
    public float bulletSpeed = 420f;
    public float bulletDiameter = 0.11f;
    public float spawnForward = 0.35f;
    public float maxBulletRange = 400f;

    public LayerMask bulletHitMask = ~0;

    [Header("屏幕")]
    public CameraController cameraShake;

    [Header("弹匣 / 换弹")]
    public int magazineSize = 30;
    public float reloadDuration = 2f;
    public KeyCode reloadKey = KeyCode.R;

    Transform _lmb, _rmb, _q, _e;
    Collider[] _selfColliders;
    int[] _ammo;
    float[] _reloadEndTime;

    void Awake()
    {
        _lmb = overrideFireLMB != null ? overrideFireLMB : rightGun;
        _rmb = overrideFireRMB != null ? overrideFireRMB : leftGun;
        _q = overrideFireQ != null ? overrideFireQ : leftShoulder;
        _e = overrideFireE != null ? overrideFireE : rightShoulder;

        _selfColliders = GetComponentsInChildren<Collider>(true);
        if (aimCamera == null)
            aimCamera = Camera.main;
        cameraShake ??= aimCamera != null ? aimCamera.GetComponent<CameraController>() : null;
        cameraShake ??= FindFirstObjectByType<CameraController>();
        if (lockOnSystem == null)
            lockOnSystem = FindFirstObjectByType<LockOnSystem>();

        _ammo = new int[4];
        _reloadEndTime = new float[4];
        for (int i = 0; i < 4; i++)
            _ammo[i] = magazineSize;
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < 4; i++)
        {
            if (_reloadEndTime[i] > 0f && t >= _reloadEndTime[i])
            {
                _ammo[i] = magazineSize;
                _reloadEndTime[i] = 0f;
            }
        }

        if (Input.GetKeyDown(reloadKey))
            TryReloadAll();

        TryFire(Input.GetMouseButtonDown(0), _lmb, 0);
        TryFire(Input.GetMouseButtonDown(1), _rmb, 1);
        TryFire(Input.GetKeyDown(KeyCode.Q), _q, 2);
        TryFire(Input.GetKeyDown(KeyCode.E), _e, 3);
    }

    public int GetMagazineAmmo(int slot)
    {
        if (_ammo == null || slot < 0 || slot >= _ammo.Length)
            return 0;
        return _ammo[slot];
    }

    public bool IsReloadingSlot(int slot)
    {
        if (_reloadEndTime == null || slot < 0 || slot >= _reloadEndTime.Length)
            return false;
        return _reloadEndTime[slot] > Time.time;
    }

    void TryReloadAll()
    {
        if (_ammo == null)
            return;
        float t = Time.time;
        for (int i = 0; i < 4; i++)
        {
            if (_reloadEndTime[i] > t)
                continue;
            if (_ammo[i] >= magazineSize)
                continue;
            _reloadEndTime[i] = t + reloadDuration;
        }
    }

    bool TryConsumeAmmo(int slot)
    {
        if (_ammo == null)
            return true;
        if (slot < 0 || slot >= _ammo.Length)
            return false;
        if (IsReloadingSlot(slot))
            return false;
        if (_ammo[slot] > 0)
        {
            _ammo[slot]--;
            return true;
        }

        BeginReload(slot);
        return false;
    }

    void BeginReload(int slot)
    {
        if (IsReloadingSlot(slot))
            return;
        if (_ammo[slot] >= magazineSize)
            return;
        _reloadEndTime[slot] = Time.time + reloadDuration;
    }

    void TryFire(bool pressed, Transform muzzle, int slotIndex)
    {
        if (!pressed || muzzle == null)
            return;
        if (!TryConsumeAmmo(slotIndex))
            return;
        FireFrom(muzzle);
    }

    void FireFrom(Transform muzzle)
    {
        Vector3 dir = GetAimDirection(muzzle.position);
        float life = maxBulletRange / Mathf.Max(bulletSpeed, 1f) + 0.5f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Bullet";
        go.layer = gameObject.layer;
        go.transform.position = muzzle.position + dir * spawnForward;
        go.transform.localScale = Vector3.one * bulletDiameter;

        var rend = go.GetComponent<Renderer>();
        ProjectileBullet.ApplyBrightBody(rend, new Color(0.25f, 0.85f, 1f), new Color(0.4f, 1.2f, 1.4f));

        var proj = go.AddComponent<ProjectileBullet>();
        Transform homingTarget = lockOnSystem != null ? lockOnSystem.currentTarget : null;
        proj.Setup(bulletSpeed, dir, _selfColliders, life, homingTarget);

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
