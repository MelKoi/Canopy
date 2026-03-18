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
    public float jumpVelocity = 7f;
    [Tooltip("起跳后短时内不触发长按升空")]
    public float jumpAscendCooldownTime = 0.25f;
    [Tooltip("起跳后：检测上移 + 竖直上推，减轻挤地；起跳仍用未抬升检测")]
    public float jumpGroundPeelDuration = 0.14f;
    public float jumpPeelUpAcceleration = 12f;
    public float jumpGroundProbeLift = 0.22f;
    [Tooltip("缓解 Update / FixedUpdate 不同步漏跳")]
    public float jumpInputBuffer = 0.12f;
    public Transform groundCheckFollowTransform;
    public Vector3 groundCheckFollowLocalOffset = new Vector3(0f, -2.7f, 0f);

    Rigidbody rb;
    MovementProfile currentProfile;
    float currentEnergy;
    Vector3 moveDir;
    Vector3 lockedOverBoostDir;

    bool isGrounded;
    bool isDodging;
    bool isOverBoosting;
    bool isBoosting;
    float _jumpAscendCooldown;
    float _peelTimer;
    float _jumpBufferUntil = -1f;
    bool _feetGroundedLastStep;

    public bool TankMode { get; private set; }
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    public bool IsSprinting => isOverBoosting;

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

        if (input.DodgePressed && currentEnergy >= dodgeProfile.energyCostPerSecond)
        {
            StartCoroutine(DodgeCoroutine());
            return;
        }

        if (input.OverBoostHeld && currentEnergy > 0f)
        {
            if (!isOverBoosting)
            {
                if (moveDir.sqrMagnitude < 0.01f)
                {
                    Vector3 f = cameraTransform.forward;
                    f.y = 0f;
                    lockedOverBoostDir = f.sqrMagnitude > 0.01f ? f.normalized : transform.forward;
                }
                else lockedOverBoostDir = moveDir;
            }
            isOverBoosting = true;
            currentProfile = overBoostProfile;
            return;
        }

        isOverBoosting = false;

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
        Vector3 tv = isOverBoosting
            ? lockedOverBoostDir * currentProfile.maxSpeed
            : moveDir * currentProfile.maxSpeed;

        float vy;
        if (!isDodging && _peelTimer > 0f && currentProfile != verticalBoostProfile)
        {
            vy = rb.velocity.y + jumpPeelUpAcceleration * Time.fixedDeltaTime;
            if (isGrounded && vy < jumpVelocity * 0.85f)
                vy = Mathf.Max(vy, jumpVelocity * 0.92f);
        }
        else if (!isGrounded && currentProfile == verticalBoostProfile)
            vy = currentProfile.maxSpeed;
        else if (!isGrounded)
        {
            float decayTo = rb.velocity.y > 0f ? 0f : rb.velocity.y + Physics.gravity.y * Time.fixedDeltaTime;
            vy = Mathf.MoveTowards(rb.velocity.y, decayTo, currentProfile.deceleration * Time.fixedDeltaTime);
        }
        else
            vy = rb.velocity.y;

        float acc = currentProfile.acceleration * Time.fixedDeltaTime;
        float dec = currentProfile.deceleration * Time.fixedDeltaTime;
        Vector2 cur = new Vector2(rb.velocity.x, rb.velocity.z);
        Vector2 tar = new Vector2(tv.x, tv.z);
        Vector2 horz = Vector2.MoveTowards(cur, tar, tar.sqrMagnitude <= cur.sqrMagnitude + 0.001f ? dec : acc);
        rb.velocity = new Vector3(horz.x, vy, horz.y);
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

        bool jumpReq = input.JumpPressed || (_jumpBufferUntil > 0f && Time.time <= _jumpBufferUntil);
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
