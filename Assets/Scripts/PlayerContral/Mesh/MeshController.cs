using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MechController : MonoBehaviour
{
    [Header("Reference")]
    public MechInput input;
    public Transform cameraTransform;

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

    [Header("Ground Check")]//地面检测
    public Transform groundCheck;
    public LayerMask groundMask;
    public float groundCheckDistance = 0.3f;

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

    #region Interface
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
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
        UpdateMoveDirection();
        UpdateState();
        RegenerateEnergy();
        Debug.Log(isGrounded);
    }

    void FixedUpdate()
    {
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

        if (input.OverBoostHeld && currentEnergy > 0f)//极速推进
        {
            if (!isOverBoosting)
            {
                lockedOverBoostDir = moveDir == Vector3.zero ? transform.forward : moveDir;
            }

            isOverBoosting = true;
            currentProfile = overBoostProfile;
            return;
        }

        isOverBoosting = false;

        if (!isGrounded && input.JumpHeld && currentEnergy > 0f)//地面监测和跳跃
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
        Vector3 camF = cameraTransform.forward;
        Vector3 camR = cameraTransform.right;
        camF.y = camR.y = 0;

        moveDir =
            camF.normalized * input.MoveAxis.y +
            camR.normalized * input.MoveAxis.x;

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

        if (!isGrounded && currentProfile == verticalBoostProfile)
        {
            targetVelocity.y = currentProfile.maxSpeed;
        }
        else
        {
            targetVelocity.y = rb.linearVelocity.y;
        }

        rb.linearVelocity = Vector3.MoveTowards(
            rb.linearVelocity,
            targetVelocity,
            currentProfile.acceleration * Time.fixedDeltaTime
        );
    }

    #endregion

    #region Jump & Ground

    void UpdateGrounded()
    {
        isGrounded = Physics.SphereCast(
        groundCheck.position,
        0.3f,
        Vector3.down,
        out _,
        groundCheckDistance,
        groundMask
        );


        if (isGrounded && input.JumpPressed)
        {
            Vector3 v = rb.linearVelocity;
            v.y = 8f;
            rb.linearVelocity = v;
        }
    }

    #endregion

    #region Dodge

    System.Collections.IEnumerator DodgeCoroutine()
    {
        isDodging = true;
        currentEnergy -= dodgeProfile.energyCostPerSecond;

        rb.linearVelocity = moveDir * dodgeProfile.maxSpeed;

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