using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MechController : MonoBehaviour
{
    [Header("Reference")]
    public MechInput input;
    public Transform cameraTransform;
    public Transform bodyTransform;

    [Header("Movement Profiles")]
    public MovementProfile groundProfile;
    public MovementProfile boostProfile;
    public MovementProfile overBoostProfile;
    public MovementProfile verticalBoostProfile;
    public MovementProfile dodgeProfile;

    [Header("Energy")]
    public float maxEnergy = 100f;
    public float energyRegen = 25f;
    public float energyRegenInSky = 5f;

    [Header("Jump & Ground")]
    public Transform groundCheck;
    public LayerMask groundMask;
    public float groundCheckRadius = 0.4f;
    public float groundCheckOffsetFromCenter = 0.6f;
    [Tooltip("起跳瞬时竖直速度；重型机甲宜 7~10（原错误用过大会像火箭）")]
    public float jumpVelocity = 8f;
    [Tooltip("起跳后短时内不触发长按升空")]
    public float jumpAscendCooldownTime = 0.25f;
    [Tooltip("起跳后：检测上移 + 竖直上推，减轻挤地；起跳仍用未抬升检测")]
    public float jumpGroundPeelDuration = 0.1f;
    [Tooltip("离地后极短时间内额外上推加速度，宜偏小以免发飘")]
    public float jumpPeelUpAcceleration = 6f;
    public float jumpGroundProbeLift = 0.22f;
    [Tooltip("缓解 Update / FixedUpdate 不同步漏跳")]
    public float jumpInputBuffer = 0.12f;
    public Transform groundCheckFollowTransform;
    public Vector3 groundCheckFollowLocalOffset = new Vector3(0f, -2.7f, 0f);

    [Header("空中 — 重型手感")]
    [Tooltip("相对项目重力的倍数，略大于 1 下落更快、更“砸地”")]
    public float airborneGravityMultiplier = 1.12f;
    [Tooltip("上升阶段额外减速（米/秒²），模拟大质量爬升阻力")]
    public float jumpAscentCounterAccel = 4f;
    [Tooltip("非垂直推进时，空中水平加/减速相对地面的比例（越小越笨重）")]
    [Range(0.15f, 1f)]
    public float airHorizontalAccelScale = 0.38f;
    [Tooltip("松开空格且按住 WASD 下落时：基础向上缓冲加速度（米/秒²），模拟姿态喷口/承托，而非仅减轻重力")]
    public float descentMoveBufferBaseAccel = 14f;
    [Tooltip("缓冲强度随坠落速度增加（每 1m/s 下落额外提供的向上加速度）")]
    public float descentMoveBufferPerFallSpeed = 1.1f;
    [Tooltip("单帧缓冲向上加速度上限，避免数值过大")]
    public float descentMoveBufferMaxCounterAccel = 32f;
    [Tooltip("缓冲生效时的最大坠落速度（米/秒，正值表示 |vy| 上限）；0 表示不限制")]
    public float descentMoveBufferTerminalFallSpeed = 5.5f;
    [Tooltip("缓冲加速度平滑时间（秒），略大更有“机体先承住再稳住”的感觉")]
    public float descentMoveBufferSmoothTime = 0.14f;

    Rigidbody rb;
    MovementProfile currentProfile;
    float currentEnergy;
    Vector3 moveDir;
    bool isGrounded;
    bool isDodging;
    bool isOverBoosting;
    bool isBoosting;
    float _jumpAscendCooldown;
    float _peelTimer;
    float _jumpBufferUntil = -1f;
    bool _feetGroundedLastStep;
    bool _overBoostModeActive;
    float _descentBufferCushionSmoothed;
    float _descentBufferCushionVel;
    int _weaponMovementLockCount;

    public bool TankMode { get; private set; }
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    public bool IsSprinting => isOverBoosting;
    /// <summary>快速推进（Ctrl 触发的地面加速段），用于镜头 FOV 等。</summary>
    public bool IsQuickBoosting => isBoosting && !isOverBoosting && !isDodging;

    public bool IsWeaponMovementLocked => _weaponMovementLockCount > 0;

    /// <summary>武器开火等逻辑可嵌套调用：Push/Pop 成对使用。锁定期间清空速度且不响应移动/推进/跳跃输入。</summary>
    public void PushWeaponMovementLock() => _weaponMovementLockCount++;

    public void PopWeaponMovementLock() => _weaponMovementLockCount = Mathf.Max(0, _weaponMovementLockCount - 1);

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        currentEnergy = maxEnergy;
        currentProfile = groundProfile;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (_jumpAscendCooldown > 0f)
            _jumpAscendCooldown -= Time.deltaTime;
        if (input.TurnModeTogglePressed) TankMode = !TankMode;
        if (input.JumpPressed && jumpInputBuffer > 0f)
            _jumpBufferUntil = Time.time + jumpInputBuffer;
        UpdateMoveDirection();
        UpdateState();
        RegenerateEnergy();
    }

    void FixedUpdate()
    {
        if (_overBoostModeActive && currentEnergy <= 0f)
            _overBoostModeActive = false;
        SyncGroundCheckWithBody();
        UpdateGroundAndJump();
        ApplyMovement();
        ConsumeEnergy();
        if (_peelTimer > 0f)
            _peelTimer = Mathf.Max(0f, _peelTimer - Time.fixedDeltaTime);
    }

    void UpdateState()
    {
        if (isDodging) return;
        if (IsWeaponMovementLocked)
            return;

        if (input.DodgePressed && currentEnergy >= dodgeProfile.energyCostPerSecond)
        {
            StartCoroutine(DodgeCoroutine());
            return;
        }

        if (input.OverBoostTogglePressed)
        {
            if (_overBoostModeActive)
                _overBoostModeActive = false;
            else if (currentEnergy > 0f)
                _overBoostModeActive = true;
        }

        if (_overBoostModeActive && currentEnergy > 0f)
        {
            isOverBoosting = true;
            currentProfile = overBoostProfile;
            return;
        }

        isOverBoosting = false;
        _overBoostModeActive = false;

        if (!isGrounded && !_feetGroundedLastStep && input.JumpHeld && currentEnergy > 0f && _jumpAscendCooldown <= 0f)
        {
            currentProfile = verticalBoostProfile;
            return;
        }

        if (input.BoostPressed)
        {
            isBoosting = true;
            return;
        }
        if (isBoosting)
            currentProfile = boostProfile;
        else
            currentProfile = groundProfile;
        if (input.MoveAxis == Vector2.zero)
            isBoosting = false;
    }

    void UpdateMoveDirection()
    {
        Vector3 f, r;
        if (TankMode || bodyTransform == null)
        {
            f = cameraTransform.forward;
            r = cameraTransform.right;
        }
        else
        {
            f = bodyTransform.forward;
            r = bodyTransform.right;
        }
        f.y = r.y = 0f;
        if (f.sqrMagnitude > 0.01f) f.Normalize();
        if (r.sqrMagnitude > 0.01f) r.Normalize();
        moveDir = f * input.MoveAxis.y + r * input.MoveAxis.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
    }

    void ApplyMovement()
    {
        if (IsWeaponMovementLocked)
        {
            rb.velocity = Vector3.zero;
            return;
        }

        if (isOverBoosting)
        {
            Vector3 dir = GetOverBoostThrustDirection();
            Vector3 overBoostTargetVel = dir * currentProfile.maxSpeed;
            float step = currentProfile.acceleration * Time.fixedDeltaTime;
            rb.velocity = Vector3.MoveTowards(rb.velocity, overBoostTargetVel, step);
            return;
        }

        Vector3 tv = moveDir * currentProfile.maxSpeed;

        float vy;
        if (!isDodging && _peelTimer > 0f && currentProfile != verticalBoostProfile)
        {
            vy = rb.velocity.y + jumpPeelUpAcceleration * Time.fixedDeltaTime;
            if (isGrounded && vy < jumpVelocity * 0.55f)
                vy = Mathf.Max(vy, jumpVelocity * 0.72f);
        }
        else if (!isGrounded && currentProfile == verticalBoostProfile)
            vy = currentProfile.maxSpeed;
        else if (!isGrounded)
        {
            float g = Physics.gravity.y * airborneGravityMultiplier;
            float dt = Time.fixedDeltaTime;
            bool descentBuffer =
                !input.JumpHeld && moveDir.sqrMagnitude > 0.01f && rb.velocity.y < -0.12f;
            float cushionTarget = 0f;
            if (descentBuffer)
            {
                float fallSpeed = -rb.velocity.y;
                cushionTarget = Mathf.Min(
                    descentMoveBufferBaseAccel + fallSpeed * descentMoveBufferPerFallSpeed,
                    descentMoveBufferMaxCounterAccel);
            }

            float smoothT = descentMoveBufferSmoothTime > 0.01f ? descentMoveBufferSmoothTime : 0.01f;
            _descentBufferCushionSmoothed = Mathf.SmoothDamp(
                _descentBufferCushionSmoothed,
                cushionTarget,
                ref _descentBufferCushionVel,
                smoothT,
                Mathf.Infinity,
                dt);

            g += _descentBufferCushionSmoothed;
            vy = rb.velocity.y + g * dt;
            if (descentBuffer && descentMoveBufferTerminalFallSpeed > 0f)
                vy = Mathf.Max(vy, -descentMoveBufferTerminalFallSpeed);
            if (vy > 0f && jumpAscentCounterAccel > 0f)
                vy = Mathf.Max(0f, vy - jumpAscentCounterAccel * dt);
        }
        else
        {
            vy = rb.velocity.y;
            _descentBufferCushionSmoothed = 0f;
            _descentBufferCushionVel = 0f;
        }

        float horizScale = 1f;
        if (!isGrounded && !isDodging && !isOverBoosting && currentProfile != verticalBoostProfile)
            horizScale = airHorizontalAccelScale;

        float acc = currentProfile.acceleration * horizScale * Time.fixedDeltaTime;
        float dec = currentProfile.deceleration * horizScale * Time.fixedDeltaTime;
        Vector2 cur = new Vector2(rb.velocity.x, rb.velocity.z);
        Vector2 tar = new Vector2(tv.x, tv.z);
        Vector2 horz = Vector2.MoveTowards(cur, tar, tar.sqrMagnitude <= cur.sqrMagnitude + 0.001f ? dec : acc);
        rb.velocity = new Vector3(horz.x, vy, horz.y);
    }

    /// <summary>极速推进方向：与机体朝向一致的默认由相机与机体对齐保证；推进中随视角（相机 forward）全向调整。</summary>
    Vector3 GetOverBoostThrustDirection()
    {
        Vector3 dir = cameraTransform != null ? cameraTransform.forward : transform.forward;
        if (bodyTransform != null && dir.sqrMagnitude < 0.0001f)
            dir = bodyTransform.forward;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;
        return dir.normalized;
    }

    void SyncGroundCheckWithBody()
    {
        if (groundCheck != null && groundCheckFollowTransform != null)
            groundCheck.position = groundCheckFollowTransform.TransformPoint(groundCheckFollowLocalOffset);
    }

    void UpdateGroundAndJump()
    {
        Vector3 p = groundCheck != null ? groundCheck.position : transform.position + Vector3.down * groundCheckOffsetFromCenter;
        bool mask = groundMask != 0;
        bool feet = mask && Physics.CheckSphere(p, groundCheckRadius, groundMask);
        if (_peelTimer > 0f && jumpGroundProbeLift > 0f)
            p.y += jumpGroundProbeLift;
        isGrounded = mask && Physics.CheckSphere(p, groundCheckRadius, groundMask);
        _feetGroundedLastStep = feet;

        bool jumpReq = !IsWeaponMovementLocked &&
                       (input.JumpPressed || (_jumpBufferUntil > 0f && Time.time <= _jumpBufferUntil));
        if (feet && jumpReq)
        {
            rb.velocity = new Vector3(rb.velocity.x, jumpVelocity, rb.velocity.z);
            _jumpAscendCooldown = jumpAscendCooldownTime;
            _peelTimer = jumpGroundPeelDuration;
            _jumpBufferUntil = -1f;
        }
    }

    System.Collections.IEnumerator DodgeCoroutine()
    {
        isDodging = true;
        currentEnergy -= dodgeProfile.energyCostPerSecond;
        rb.velocity = moveDir * dodgeProfile.maxSpeed;
        yield return new WaitForSeconds(0.3f);
        isDodging = false;
    }

    void ConsumeEnergy()
    {
        if (currentProfile.energyCostPerSecond <= 0f) return;
        currentEnergy = Mathf.Max(0f, currentEnergy - currentProfile.energyCostPerSecond * Time.fixedDeltaTime);
    }

    void RegenerateEnergy()
    {
        if (currentProfile.energyCostPerSecond > 0f) return;
        float rate = isGrounded ? energyRegen : energyRegenInSky;
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + rate * Time.deltaTime);
    }
}
