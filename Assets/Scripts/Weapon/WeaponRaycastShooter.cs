using UnityEngine;

/// <summary>
/// 四路武器：鼠标左键、右键与 Q、E；默认左键右手枪、右键左手枪、Q 左肩、E 右肩。
/// 球形弹体点射；屏幕震动由主相机上 CameraController 处理。
/// </summary>
public class WeaponRaycastShooter : MonoBehaviour
{
    [Header("发射点")]
    public Transform front;
    public Transform leftGun;
    public Transform rightGun;
    public Transform leftShoulder;
    public Transform rightShoulder;

    [Tooltip("覆盖默认映射；不填则左键对右手枪等")]
    public Transform overrideFireLMB;
    public Transform overrideFireRMB;
    public Transform overrideFireQ;
    public Transform overrideFireE;

    [Header("瞄准")]
    [Tooltip("屏幕中心射线，一般填 Main Camera")]
    public Camera aimCamera;

    [Header("弹体")]
    [Range(300f, 500f)]
    public float bulletSpeed = 420f;
    public float bulletDiameter = 0.06f;
    public float spawnForward = 0.35f;
    public float maxBulletRange = 400f;

    public LayerMask bulletHitMask = ~0;

    [Header("屏幕震动")]
    public CameraController cameraShake;

    Transform _lmb, _rmb, _q, _e;
    Collider[] _selfColliders;

    void Awake()
    {
        _lmb = overrideFireLMB != null ? overrideFireLMB : rightGun;
        _rmb = overrideFireRMB != null ? overrideFireRMB : leftGun;
        _q = overrideFireQ != null ? overrideFireQ : leftShoulder;
        _e = overrideFireE != null ? overrideFireE : rightShoulder;

        _selfColliders = GetComponentsInChildren<Collider>(true);
        if (aimCamera == null)
            aimCamera = Camera.main;
        if (cameraShake == null && aimCamera != null)
            cameraShake = aimCamera.GetComponent<CameraController>();
        if (cameraShake == null)
            cameraShake = FindFirstObjectByType<CameraController>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && _lmb != null)
            FireFrom(_lmb);
        if (Input.GetMouseButtonDown(1) && _rmb != null)
            FireFrom(_rmb);
        if (Input.GetKeyDown(KeyCode.Q) && _q != null)
            FireFrom(_q);
        if (Input.GetKeyDown(KeyCode.E) && _e != null)
            FireFrom(_e);
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

        var proj = go.AddComponent<ProjectileBullet>();
        proj.Setup(bulletSpeed, dir, _selfColliders, life);

        cameraShake?.AddShootScreenShake(1f);
    }

    Vector3 GetAimDirection(Vector3 fromWorld)
    {
        if (aimCamera != null)
        {
            Ray r = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 hit = r.origin + r.direction * 2000f;
            if (Physics.Raycast(r, out RaycastHit rh, 2000f, bulletHitMask, QueryTriggerInteraction.Ignore))
                hit = rh.point;
            Vector3 d = hit - fromWorld;
            if (d.sqrMagnitude > 0.0001f)
                return d.normalized;
            return r.direction.normalized;
        }
        if (front != null)
            return front.forward;
        return transform.forward;
    }
}
