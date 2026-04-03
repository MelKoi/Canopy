using UnityEngine;

/// <summary>
/// 软锁：敌人在准星视口半径内时获得 currentTarget（UI 框）。
/// 硬锁：鼠标中键切换；硬锁期间强制以该敌人为瞄准目标，且不因软锁半径丢失目标。
/// </summary>
public class LockOnSystem : MonoBehaviour
{
    public Camera cam;
    [Tooltip("准星中心 (0.5,0.5) 周围的锁定半径（视口 0~1），敌人在此范围内即软锁")]
    [Range(0.02f, 0.3f)]
    public float lockRadiusViewport = 0.08f;
    [Tooltip("敌人 Tag")]
    public string enemyTag = "Enemy";
    [Tooltip("检测敌人的世界空间半径")]
    public float detectRadius = 100f;

    [Header("硬锁")]
    [Tooltip("无软锁目标时，在此视口半径内选最近屏幕中心的敌人进入硬锁")]
    [Range(0.05f, 0.5f)]
    public float hardLockPickRadiusViewport = 0.22f;
    [Tooltip("硬锁目标过远则自动解除（米）")]
    public float hardLockMaxDistance = 120f;

    Transform _softTarget;
    Transform _hardTarget;
    bool _hardLocked;

    /// <summary>武器与瞄准使用的目标：硬锁优先，否则软锁。</summary>
    public Transform currentTarget
    {
        get
        {
            if (_hardLocked && _hardTarget != null)
                return _hardTarget;
            return _softTarget;
        }
    }

    public bool IsHardLocked => _hardLocked && _hardTarget != null;
    public Transform HardLockTarget => _hardLocked ? _hardTarget : null;

    float LockRadiusSq => lockRadiusViewport * lockRadiusViewport;
    float HardPickRadiusSq => hardLockPickRadiusViewport * hardLockPickRadiusViewport;

    float ViewportDistSqFromCenter(Vector3 viewportPos)
    {
        float dx = viewportPos.x - 0.5f;
        float dy = viewportPos.y - 0.5f;
        return dx * dx + dy * dy;
    }

    void Update()
    {
        if (cam == null)
            cam = Camera.main;

        if (Input.GetMouseButtonDown(2))
            ToggleHardLock();

        if (!_hardLocked && _softTarget == null)
            DetectSoftEnemy();

        if (_hardLocked)
            ValidateHardLock();
    }

    void LateUpdate()
    {
        if (_hardLocked)
            return;

        if (_softTarget == null || cam == null)
            return;

        Vector3 viewportPos = cam.WorldToViewportPoint(_softTarget.position);
        if (viewportPos.z < 0f)
        {
            _softTarget = null;
            return;
        }

        float dx = viewportPos.x - 0.5f;
        float dy = viewportPos.y - 0.5f;
        if (dx * dx + dy * dy > LockRadiusSq)
            _softTarget = null;
    }

    void ToggleHardLock()
    {
        if (_hardLocked)
        {
            ReleaseHardLock();
            return;
        }

        Transform pick = _softTarget != null ? _softTarget : PickEnemyForHardLock();
        if (pick == null)
            return;

        _hardTarget = pick;
        _hardLocked = true;
    }

    void ReleaseHardLock()
    {
        _hardLocked = false;
        _hardTarget = null;
    }

    void ValidateHardLock()
    {
        if (_hardTarget == null || !_hardTarget.gameObject.activeInHierarchy)
        {
            ReleaseHardLock();
            return;
        }

        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(_hardTarget.position);
            if (vp.z < 0f)
            {
                ReleaseHardLock();
                return;
            }
        }

        float d = Vector3.Distance(transform.position, _hardTarget.position);
        if (d > hardLockMaxDistance)
            ReleaseHardLock();
    }

    void DetectSoftEnemy()
    {
        if (cam == null)
            return;

        Collider[] all = Physics.OverlapSphere(transform.position, detectRadius);
        float r2 = LockRadiusSq;
        float bestDist2 = float.MaxValue;
        Transform best = null;

        foreach (var c in all)
        {
            if (!c.CompareTag(enemyTag))
                continue;

            Vector3 vp = cam.WorldToViewportPoint(c.transform.position);
            if (vp.z < 0f)
                continue;

            float d2 = ViewportDistSqFromCenter(vp);
            if (d2 <= r2 && d2 < bestDist2)
            {
                bestDist2 = d2;
                best = c.transform;
            }
        }

        if (best != null)
            _softTarget = best;
    }

    Transform PickEnemyForHardLock()
    {
        if (cam == null)
            return null;

        Collider[] all = Physics.OverlapSphere(transform.position, detectRadius);
        float r2 = HardPickRadiusSq;
        float bestDist2 = float.MaxValue;
        Transform best = null;

        foreach (var c in all)
        {
            if (!c.CompareTag(enemyTag))
                continue;

            Vector3 vp = cam.WorldToViewportPoint(c.transform.position);
            if (vp.z < 0f)
                continue;

            float d2 = ViewportDistSqFromCenter(vp);
            if (d2 > r2)
                continue;

            if (d2 < bestDist2)
            {
                bestDist2 = d2;
                best = c.transform;
            }
        }

        return best;
    }
}
