using UnityEngine;

/// <summary>
/// ??°§???????????/???Q/E????? ????????????????????????Q????≥ÜE????≥á
/// ???¶»??·œ????????????????????? CameraController ??????
/// </summary>
public class WeaponRaycastShooter : MonoBehaviour
{
    [Header("?????")]
    public Transform front;
    public Transform leftGun;
    public Transform rightGun;
    public Transform leftShoulder;
    public Transform rightShoulder;

    [Tooltip("???????????????????????rightGun ??")]
    public Transform overrideFireLMB;
    public Transform overrideFireRMB;
    public Transform overrideFireQ;
    public Transform overrideFireE;

    [Header("???")]
    [Tooltip("????????????????? Main Camera")]
    public Camera aimCamera;

    [Header("????")]
    [Range(300f, 500f)]
    public float bulletSpeed = 420f;
    public float bulletDiameter = 0.06f;
    public float spawnForward = 0.35f;
    public float maxBulletRange = 400f;

    public LayerMask bulletHitMask = ~0;

    [Header("?????")]
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
