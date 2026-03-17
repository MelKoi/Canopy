using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MechController : MonoBehaviour
{
    [Header("Reference")]
    public MechInput input;
    public Transform cameraTransform;
    /// <summary>机体转向时用于朝向与移动方向的机体（如 DownBody），坦克式时可不设</summary>
    public Transform bodyTransform;

    [Header("Movement Profiles")]//移动参数配置
    public MovementProfile groundProfile;
    public MovementProfile boostProfile;
    public MovementProfile overBoostProfile;
    public MovementProfile verticalBoostProfile;
    public MovementProfile dodgeProfile;

    [Header("Energy")]//能量配置
    public float maxEnergy = 100f;
    public float energyRegen = 25f;
    public float energyRegenInSky = 5f;

    [Header("Ground Check")]//地面检测（LayerMask 勾选 Ground，地面物体设为 Ground 层）
    public Transform groundCheck;
    public LayerMask groundMask;
    [Tooltip("检测球半径，站在地面时球体需能碰到 Ground 层；若触地不稳可适当调大")]
    public float groundCheckRadius = 0.4f;
    [Tooltip("未指定 groundCheck 时，用机体中心向下此偏移作为检测点（脚底附近）")]
    public float groundCheckOffsetFromCenter = 0.6f;
    [Tooltip("地面起跳初速度，可适当调大防止吸附感")]
    public float jumpVelocity = 7f;
    [Tooltip("地面起跳后此时间内不进入「长按升空」，避免点按也扣能")]
    public float jumpAscendCooldownTime = 0.25f;

    Rigidbody rb;
    MovementProfile currentProfile;//当前的速度参数

    float currentEnergy;//当前的能量
    //？
    Vector3 moveDir;
    Vector3 lockedOverBoostDir;

    bool isGrounded;
    bool isDodging;
    bool isOverBoosting;
    bool isBoosting;
    float _jumpAscendCooldown; // 起跳后一段时间内不触发长按升空
    bool _justAppliedJump;     // 本帧已施加起跳，避免同帧 ApplyMovement 把 vy 衰减掉

    /// <summary>true=坦克式（镜头转机体不转），false=机体转向（A/D 转机体，镜头跟随）</summary>
    public bool TankMode { get; private set; }

    #region Interface
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    /// <summary>当前是否处于极速推进/冲刺状态（Tab）</summary>
    public bool IsSprinting => isOverBoosting;
    #endregion

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
        if (input.TurnModeTogglePressed)
            TankMode = !TankMode;
        UpdateMoveDirection();
        UpdateState();
        RegenerateEnergy();
    }

    void FixedUpdate()
    {
        _justAppliedJump = false;
        UpdateGrounded();
        ApplyMovement();
        ConsumeEnergy();
    }

    #region State

    void UpdateState()
    {
        if (isDodging) return;

        if (input.DodgePressed && currentEnergy >= dodgeProfile.energyCostPerSecond)//按下闪避且能量能够用来闪避
        {
            StartCoroutine(DodgeCoroutine());
            return;
        }

        if (input.OverBoostHeld && currentEnergy > 0f) // 按 Tab 极速推进（冲刺）
        {
            if (!isOverBoosting)
            {
                // 没按 WASD 时朝镜头方向（往前）冲刺，否则按当前移动方向
                if (moveDir.sqrMagnitude < 0.01f)
                {
                    Vector3 camFwd = cameraTransform.forward;
                    camFwd.y = 0;
                    lockedOverBoostDir = camFwd.sqrMagnitude > 0.01f ? camFwd.normalized : transform.forward;
                }
                else
                {
                    lockedOverBoostDir = moveDir;
                }
            }
            isOverBoosting = true;
            currentProfile = overBoostProfile;
            return;
        }

        isOverBoosting = false;

        // 长按空格升空：仅在空中且未处于「起跳冷却」时扣能升空，点按空格只做地面起跳
        if (!isGrounded && input.JumpHeld && currentEnergy > 0f && _jumpAscendCooldown <= 0f)
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
        {
            currentProfile = boostProfile;
        }
        else
        {
            currentProfile = groundProfile;
        }
        if (input.MoveAxis == Vector2.zero)
        {
            isBoosting = false;
        }
    }

    #endregion

    #region Movement

    void UpdateMoveDirection()
    {
        if (TankMode)
        {
            // 坦克式：机体不转，按镜头方向移动
            Vector3 camF = cameraTransform.forward;
            Vector3 camR = cameraTransform.right;
            camF.y = camR.y = 0;
            moveDir = camF.normalized * input.MoveAxis.y + camR.normalized * input.MoveAxis.x;
        }
        else
        {
            // 机体转向：移动沿机体前后左右（W/S 前后，A/D 左右）
            if (bodyTransform != null)
            {
                Vector3 fwd = bodyTransform.forward;
                Vector3 right = bodyTransform.right;
                fwd.y = right.y = 0;
                if (fwd.sqrMagnitude > 0.01f) fwd = fwd.normalized;
                if (right.sqrMagnitude > 0.01f) right = right.normalized;
                moveDir = fwd * input.MoveAxis.y + right * input.MoveAxis.x;
            }
            else
            {
                Vector3 camF = cameraTransform.forward;
                Vector3 camR = cameraTransform.right;
                camF.y = camR.y = 0;
                moveDir = camF.normalized * input.MoveAxis.y + camR.normalized * input.MoveAxis.x;
            }
        }
        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();
    }

    void ApplyMovement()
    {
        Vector3 targetVelocity = Vector3.zero;

        if (isOverBoosting)
        {
            targetVelocity = lockedOverBoostDir * currentProfile.maxSpeed;
        }
        else
        {
            targetVelocity = moveDir * currentProfile.maxSpeed;
        }

        // 竖直方向：升空时用加速度推向目标速度，松开升空时用减速度快速衰减（增加重量感）
        if (!isGrounded && currentProfile == verticalBoostProfile)
        {
            targetVelocity.y = currentProfile.maxSpeed;
        }
        else if (!isGrounded)
        {
            // 空中且未在升空：向上速度快速衰减；若本帧刚起跳则保留 vy 不衰减
            if (_justAppliedJump)
                targetVelocity.y = rb.velocity.y;
            else
            {
                float decayTarget = rb.velocity.y > 0f ? 0f : (rb.velocity.y + Physics.gravity.y * Time.fixedDeltaTime);
                targetVelocity.y = Mathf.MoveTowards(rb.velocity.y, decayTarget, currentProfile.deceleration * Time.fixedDeltaTime);
            }
        }
        else
        {
            targetVelocity.y = rb.velocity.y;
        }

        // 水平：加速时用 acceleration（瞬间响应），减速/松键时用 deceleration（松键即停，保留极小惯性）
        float acc = currentProfile.acceleration * Time.fixedDeltaTime;
        float dec = currentProfile.deceleration * Time.fixedDeltaTime;
        Vector2 curHorz = new Vector2(rb.velocity.x, rb.velocity.z);
        Vector2 tarHorz = new Vector2(targetVelocity.x, targetVelocity.z);
        float stepHorz = (tarHorz.sqrMagnitude <= curHorz.sqrMagnitude + 0.001f) ? dec : acc;
        Vector2 nextHorz = Vector2.MoveTowards(curHorz, tarHorz, stepHorz);

        Vector3 nextVel;
        nextVel.x = nextHorz.x;
        nextVel.z = nextHorz.y;
        nextVel.y = targetVelocity.y; // 地面保持 vy；空中为升空目标或衰减结果
        rb.velocity = nextVel;
    }

    #endregion

    #region Jump & Ground

    void UpdateGrounded()
    {
        // 用重叠球检测：站在地面时脚底球体与 Ground 层碰撞体重叠即判为触地
        Vector3 checkPos = groundCheck != null ? groundCheck.position : transform.position + Vector3.down * groundCheckOffsetFromCenter;
        if (groundMask != 0)
            isGrounded = Physics.CheckSphere(checkPos, groundCheckRadius, groundMask);
        else
            isGrounded = false;

        if (isGrounded && input.JumpPressed)
        {
            Vector3 v = rb.velocity;
            v.y = jumpVelocity;
            rb.velocity = v;
            _jumpAscendCooldown = jumpAscendCooldownTime;
            _justAppliedJump = true; // 防止本帧后续 ApplyMovement 把起跳速度衰减掉
        }
    }

    #endregion

    #region Dodge

    System.Collections.IEnumerator DodgeCoroutine()
    {
        isDodging = true;
        currentEnergy -= dodgeProfile.energyCostPerSecond;

        rb.velocity = moveDir * dodgeProfile.maxSpeed;

        yield return new WaitForSeconds(0.3f);
        isDodging = false;
    }

    #endregion

    #region Energy

    void ConsumeEnergy()
    {
        if (currentProfile.energyCostPerSecond <= 0f) return;

        currentEnergy -= currentProfile.energyCostPerSecond * Time.fixedDeltaTime;
        currentEnergy = Mathf.Max(0f, currentEnergy);
    }

    void RegenerateEnergy()
    {
        if (currentProfile.energyCostPerSecond > 0f) return;
        if (isGrounded)
            currentEnergy += energyRegen * Time.deltaTime;
        else if (!isGrounded)
            currentEnergy += energyRegenInSky * Time.deltaTime;

        currentEnergy = Mathf.Min(maxEnergy, currentEnergy);
    }

    #endregion
}