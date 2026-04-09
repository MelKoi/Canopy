using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第三人称相机：构图偏移、旋转阻尼、Boost FOV/后拉。
/// 硬锁仅由 LockOnSystem 负责目标与武器瞄准，不改变摄像机运动（避免切入另一套机位/俯视）。
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Input State")]
    public bool enableInput = true;

    [Header("Target & Params")]
    public Transform target;
    public Vector2 sensitivity = new Vector2(0.114f, 0.114f);
    public Vector2 verticalClamp = new Vector2(-30f, 60f);
    [Tooltip("相对旋转后的机位偏移；默认略右、略高，使机体落在画面左下区域")]
    public Vector3 offset = new Vector3(0.38f, 1.28f, -11.5f);

    [Header("旋转阻尼")]
    [Tooltip("鼠标位移小于此像素则视为死区")]
    public float mouseDeadZonePixels = 0.8f;
    [Tooltip("Yaw 目标跟随平滑时间（秒），越小越跟手")]
    public float yawSmoothTime = 0.09f;
    [Tooltip("Pitch 平滑时间（秒）")]
    public float pitchSmoothTime = 0.08f;
    [Tooltip("冲刺 / 推进时旋转阻尼略增（更有重量感）")]
    public float yawSmoothTimeBoostMul = 1.45f;
    public float pitchSmoothTimeBoostMul = 1.35f;

    [Header("垂直运动 — 构图跟随")]
    [Tooltip("按竖直速度（m/s）叠加的俯仰偏移比例（度 / m/s）；上升为负（略抬头）、下落为正（略低头），速度归零时偏移会收回")]
    public float verticalMotionPitchOffsetPerSpeed = 1.65f;
    [Tooltip("上述构图俯仰偏移的上限（度）")]
    public float verticalMotionPitchFramingMaxDeg = 14f;
    [Tooltip("构图俯仰偏移的平滑时间（秒）")]
    public float verticalMotionPitchFramingSmoothTime = 0.11f;
    [Tooltip("竖直速度超过此值（m/s）开始缩短 pitch 平滑时间")]
    public float verticalMotionTightenSpeedStart = 2.5f;
    [Tooltip("竖直速度达到此值（m/s）时平滑时间乘数取到最小")]
    public float verticalMotionTightenSpeedFull = 12f;
    [Tooltip("急升急降时 pitch 平滑时间最小乘数（越小镜头俯仰跟得越快）")]
    [Range(0.15f, 1f)]
    public float verticalMotionPitchSmoothMinMul = 0.38f;
    [Tooltip("急升急降时 yaw 平滑时间乘数（略收紧可减少斜向甩镜）")]
    [Range(0.35f, 1f)]
    public float verticalMotionYawSmoothMul = 0.72f;

    [Header("软锁 — 镜头吸附（默认关闭以免与准星冲突）")]
    public LockOnSystem lockOnSystem;
    [Tooltip("为 true 时：只要存在锁定目标（软锁或硬锁）就不做镜头吸附，避免对准敌人时被拽开")]
    public bool disableCameraAssistWhileLocked = true;
    [Tooltip("吸附强度（/秒）；仅在 disableCameraAssistWhileLocked=false 且非硬锁时可能生效")]
    public float softLockAssistStrength = 1.1f;
    [Tooltip("鼠标移动超过此像素/帧时不做吸附")]
    public float softLockAssistMaxMouseDelta = 1.25f;

    [Header("Boost 机位")]
    [Tooltip("推进时沿机位后轴额外拉远（米）")]
    public float boostOffsetPullBack = 1.35f;
    [Tooltip("Boost 后拉平滑时间")]
    public float boostPullSmoothTime = 0.12f;

    [Header("机体转向（预留）")]
    public float bodyTurnYawFollowSmoothTime = 0.08f;
    public MechController mechController;

    [Header("FOV")]
    public float normalFOV = 60f;
    public float sprintFOV = 68f;
    [Tooltip("快速推进（Ctrl）时额外 FOV")]
    public float quickBoostFOVBonus = 2.5f;
    public float fovLerpSpeed = 8f;
    public Camera cameraForFOV;

    [Header("射击震动")]
    public Vector3 shootShakeImpulseEuler = new Vector3(0.38f, 0.3f, 0.14f);
    public float shootShakeDecay = 16f;

    Camera _cam;
    Vector3 _shootShakeEuler;
    float _yaw;
    float _pitch;
    float _yawTarget;
    float _pitchTarget;
    float _yawVel;
    float _pitchVel;
    bool _hasSyncedFromCamera;

    float _boostPullVel;
    float _currentBoostPull;
    Rigidbody _targetRb;
    float _verticalFramingPitchOffset;
    float _verticalFramingPitchVel;

    void Awake()
    {
        _cam = cameraForFOV != null ? cameraForFOV : GetComponentInChildren<Camera>();
        if (_cam != null && normalFOV <= 0f)
            normalFOV = _cam.fieldOfView;
        if (mechController == null)
            mechController = FindFirstObjectByType<MechController>();
        if (lockOnSystem == null)
            lockOnSystem = FindFirstObjectByType<LockOnSystem>();
        if (target != null)
            _targetRb = target.GetComponentInParent<Rigidbody>();
    }

    void LateUpdate()
    {
        if (!_hasSyncedFromCamera)
            return;

        RunFreeCamera();
        ApplyFovAndShake();
    }

    void RunFreeCamera()
    {
        float yawSt = yawSmoothTime;
        float pitchSt = pitchSmoothTime;
        if (IsBoostingForCameraEffects())
        {
            yawSt *= yawSmoothTimeBoostMul;
            pitchSt *= pitchSmoothTimeBoostMul;
        }

        float vy = _targetRb != null ? _targetRb.velocity.y : 0f;
        float vyAbs = Mathf.Abs(vy);
        if (vyAbs > verticalMotionTightenSpeedStart && verticalMotionTightenSpeedFull > verticalMotionTightenSpeedStart)
        {
            float t = Mathf.InverseLerp(verticalMotionTightenSpeedStart, verticalMotionTightenSpeedFull, vyAbs);
            float pitchMul = Mathf.Lerp(1f, verticalMotionPitchSmoothMinMul, t);
            float yawMul = Mathf.Lerp(1f, verticalMotionYawSmoothMul, t);
            pitchSt *= pitchMul;
            yawSt *= yawMul;
        }

        Vector2 mouseDeltaFree = Vector2.zero;
        if (enableInput && Mouse.current != null)
        {
            Vector2 md = Mouse.current.delta.ReadValue();
            if (Mathf.Abs(md.x) < mouseDeadZonePixels) md.x = 0f;
            if (Mathf.Abs(md.y) < mouseDeadZonePixels) md.y = 0f;
            mouseDeltaFree = md;

            _yawTarget += md.x * sensitivity.x;
            _pitchTarget -= md.y * sensitivity.y;
            _pitchTarget = Mathf.Clamp(_pitchTarget, verticalClamp.x, verticalClamp.y);
        }

        ApplySoftLockAssist(mouseDeltaFree);

        if (ShouldApplyVerticalMotionAssist())
        {
            float wantOffset = Mathf.Clamp(
                -vy * verticalMotionPitchOffsetPerSpeed,
                -verticalMotionPitchFramingMaxDeg,
                verticalMotionPitchFramingMaxDeg);
            float fst = verticalMotionPitchFramingSmoothTime > 0.01f ? verticalMotionPitchFramingSmoothTime : 0.01f;
            _verticalFramingPitchOffset = Mathf.SmoothDamp(
                _verticalFramingPitchOffset,
                wantOffset,
                ref _verticalFramingPitchVel,
                fst,
                Mathf.Infinity,
                Time.deltaTime);
        }
        else
        {
            _verticalFramingPitchOffset = Mathf.SmoothDamp(
                _verticalFramingPitchOffset,
                0f,
                ref _verticalFramingPitchVel,
                0.07f,
                Mathf.Infinity,
                Time.deltaTime);
        }

        _yaw = Mathf.SmoothDampAngle(_yaw, _yawTarget, ref _yawVel, yawSt, Mathf.Infinity, Time.deltaTime);
        _pitch = Mathf.SmoothDamp(_pitch, _pitchTarget, ref _pitchVel, pitchSt, Mathf.Infinity, Time.deltaTime);

        float pullTarget = IsBoostingForCameraEffects() ? boostOffsetPullBack : 0f;
        _currentBoostPull = Mathf.SmoothDamp(_currentBoostPull, pullTarget, ref _boostPullVel, boostPullSmoothTime, Mathf.Infinity, Time.deltaTime);

        float displayPitch = Mathf.Clamp(_pitch + _verticalFramingPitchOffset, verticalClamp.x, verticalClamp.y);
        Quaternion baseRot = Quaternion.Euler(displayPitch, _yaw, 0f);
        Vector3 effectiveOffset = offset + new Vector3(0f, 0f, -_currentBoostPull);
        _shootShakeEuler = Vector3.Lerp(_shootShakeEuler, Vector3.zero, Time.deltaTime * shootShakeDecay);
        Quaternion rotation = baseRot * Quaternion.Euler(_shootShakeEuler);

        if (transform.parent != null)
        {
            transform.localPosition = baseRot * effectiveOffset;
            transform.localRotation = rotation;
        }
        else
        {
            if (target == null)
                return;
            transform.position = target.position + baseRot * effectiveOffset;
            transform.rotation = rotation;
        }
    }

    void ApplySoftLockAssist(Vector2 mouseDeltaThisFrame)
    {
        if (lockOnSystem == null)
            return;

        if (lockOnSystem.IsHardLocked)
            return;

        if (disableCameraAssistWhileLocked && lockOnSystem.currentTarget != null)
            return;

        if (mouseDeltaThisFrame.sqrMagnitude > softLockAssistMaxMouseDelta * softLockAssistMaxMouseDelta)
            return;

        Transform aim = lockOnSystem.currentTarget;
        if (aim == null || target == null)
            return;

        Transform parent = transform.parent;
        if (parent == null)
            return;

        Vector3 toEnemy = aim.position - transform.position;
        if (toEnemy.sqrMagnitude < 0.01f)
            return;

        Vector3 d = parent.InverseTransformDirection(toEnemy.normalized);
        float wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float wantPitch = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
        wantPitch = Mathf.Clamp(wantPitch, verticalClamp.x, verticalClamp.y);

        float k = 1f - Mathf.Exp(-softLockAssistStrength * Time.deltaTime);
        _yawTarget = Mathf.LerpAngle(_yawTarget, wantYaw, k);
        _pitchTarget = Mathf.Lerp(_pitchTarget, wantPitch, k);
    }

    bool IsBoostingForCameraEffects()
    {
        if (mechController == null)
            return false;
        return mechController.IsSprinting || mechController.IsQuickBoosting;
    }

    bool ShouldApplyVerticalMotionAssist()
    {
        if (_targetRb == null)
            return false;
        if (lockOnSystem == null)
            return true;
        if (lockOnSystem.IsHardLocked)
            return false;
        if (disableCameraAssistWhileLocked && lockOnSystem.currentTarget != null)
            return false;
        return true;
    }

    void ApplyFovAndShake()
    {
        if (_cam == null)
            return;

        float fovGoal = normalFOV;
        if (mechController != null)
        {
            if (mechController.IsSprinting)
                fovGoal = sprintFOV;
            else if (mechController.IsQuickBoosting)
                fovGoal = normalFOV + quickBoostFOVBonus;
        }

        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fovGoal, Time.deltaTime * fovLerpSpeed);
    }

    /// <summary>入场动画结束后调用，从当前 Transform 同步内部角度与目标。</summary>
    public void ResetViewFromCurrentCamera()
    {
        if (transform.parent != null)
        {
            offset = transform.localPosition;
            _yaw = _yawTarget = transform.localEulerAngles.y;
            float rawPitch = transform.localEulerAngles.x;
            if (rawPitch > 180f) rawPitch -= 360f;
            _pitch = _pitchTarget = Mathf.Clamp(rawPitch, verticalClamp.x, verticalClamp.y);
        }
        else
        {
            if (target == null)
                return;
            Vector3 toCam = transform.position - target.position;
            float distance = toCam.magnitude;
            if (distance < 0.001f)
                return;
            Vector3 dir = toCam / distance;
            float y = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float p = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            p = Mathf.Clamp(p, verticalClamp.x, verticalClamp.y);
            _yaw = _yawTarget = y;
            _pitch = _pitchTarget = p;
            Quaternion invRot = Quaternion.Inverse(Quaternion.Euler(_pitch, _yaw, 0f));
            offset = invRot * toCam;
        }

        _yawVel = _pitchVel = 0f;
        _verticalFramingPitchOffset = 0f;
        _verticalFramingPitchVel = 0f;
        _hasSyncedFromCamera = true;
    }

    public void ResetView(float newYaw, float newPitch)
    {
        _yaw = _yawTarget = newYaw;
        _pitch = _pitchTarget = Mathf.Clamp(newPitch, verticalClamp.x, verticalClamp.y);
        _yawVel = _pitchVel = 0f;
        _verticalFramingPitchOffset = 0f;
        _verticalFramingPitchVel = 0f;
    }

    public void AddShootScreenShake(float strength = 1f)
    {
        _shootShakeEuler.x += (Random.value * 2f - 1f) * shootShakeImpulseEuler.x * strength;
        _shootShakeEuler.y += (Random.value * 2f - 1f) * shootShakeImpulseEuler.y * strength;
        _shootShakeEuler.z += (Random.value * 2f - 1f) * shootShakeImpulseEuler.z * strength;
    }
}
