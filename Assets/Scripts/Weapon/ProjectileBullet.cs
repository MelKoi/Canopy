using UnityEngine;

/// <summary>
/// 球形弹体：直线飞行，可选轻微制导；碰撞后销毁；对敌时优先沿碰撞体父链查找 <see cref="EnemyHitFeedback"/> 扣真实伤害，否则再按 Enemy 标签兜底；带尾迹可视化。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ProjectileBullet : MonoBehaviour
{
    float _maxLifetime = 3f;
    float _speed;
    Transform _homingTarget;
    Rigidbody _rb;
    bool _damagePlayerLikeEnemy;
    int _playerHealthDamage = -1;
    int _playerToughnessDelta = -1;
    [Tooltip("制导强度，越大子弹越会拐向锁定目标")]
    const float HomingStrength = 4f;

    Vector3 _baseFireDirection;
    bool _useMovementDrift;
    Vector3 _shooterMoveHorizontal;
    float _movementDriftLateralAmplitude;
    float _movementDriftWobbleHz;
    float _movementDriftSlerp;
    float _enemyDamage = 1f;

    public void Setup(float speed, Vector3 direction, Collider[] ignoreColliders, float maxLifetime,
        Transform homingTarget = null, bool damagePlayerLikeEnemy = false, int playerHealthDamage = -1,
        int playerToughnessDelta = -1,
        Vector3 shooterHorizontalMoveDirection = default, bool useMovementDriftForPlayerBullet = false,
        float movementDriftLateralAmplitude = 0.14f, float movementDriftWobbleHz = 2f, float movementDriftSteer = 5f,
        float enemyDamage = 1f)
    {
        _maxLifetime = maxLifetime;
        _speed = speed;
        _baseFireDirection = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
        _useMovementDrift = damagePlayerLikeEnemy && useMovementDriftForPlayerBullet;
        _shooterMoveHorizontal = shooterHorizontalMoveDirection;
        _shooterMoveHorizontal.y = 0f;
        if (_shooterMoveHorizontal.sqrMagnitude > 1e-6f)
            _shooterMoveHorizontal.Normalize();
        _movementDriftLateralAmplitude = movementDriftLateralAmplitude;
        _movementDriftWobbleHz = movementDriftWobbleHz;
        _movementDriftSlerp = movementDriftSteer;
        _homingTarget = _useMovementDrift ? null : homingTarget;
        _damagePlayerLikeEnemy = damagePlayerLikeEnemy;
        _playerHealthDamage = playerHealthDamage;
        _playerToughnessDelta = playerToughnessDelta;
        _enemyDamage = Mathf.Max(0f, enemyDamage);

        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody>();
        _rb.mass = 0.02f;
        _rb.useGravity = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.velocity = direction.normalized * speed;

        var col = GetComponent<Collider>();
        if (col != null && ignoreColliders != null)
        {
            foreach (var c in ignoreColliders)
            {
                if (c != null)
                    Physics.IgnoreCollision(col, c, true);
            }
        }

        float d = transform.localScale.x;
        var trail = gameObject.AddComponent<TrailRenderer>();
        ConfigureReadableTrail(trail, d,
            new Color(0.55f, 0.92f, 1f, 1f),
            new Color(0.2f, 0.55f, 1f, 0.05f));
    }

    /// <summary>高对比度弹体材质（URP Unlit / Lit 回退）。</summary>
    public static void ApplyBrightBody(Renderer rend, Color baseColor, Color emissionHdr)
    {
        if (rend == null)
            return;

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null)
            sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null)
            sh = Shader.Find("Unlit/Color");
        if (sh == null)
            sh = Shader.Find("Sprites/Default");

        var mat = new Material(sh);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", baseColor);
        else
            mat.color = baseColor;

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionHdr);
        }

        rend.material = mat;
    }

    public static void ConfigureReadableTrail(TrailRenderer trail, float diameter, Color start, Color end)
    {
        trail.time = 0.5f;
        trail.minVertexDistance = 0.02f;
        trail.numCapVertices = 5;
        trail.numCornerVertices = 4;
        trail.startWidth = Mathf.Max(0.04f, diameter * 1.15f);
        trail.endWidth = Mathf.Max(0.02f, diameter * 0.2f);

        Shader tsh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (tsh == null)
            tsh = Shader.Find("Particles/Standard Unlit");
        if (tsh == null)
            tsh = Shader.Find("Sprites/Default");
        trail.material = new Material(tsh);
        if (trail.material.HasProperty("_BaseColor"))
            trail.material.SetColor("_BaseColor", start);
        trail.startColor = start;
        trail.endColor = end;
    }

    float _t;

    void Update()
    {
        _t += Time.deltaTime;
        if (_t >= _maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_useMovementDrift && _rb != null)
        {
            Vector3 v = _rb.velocity;
            Vector3 dir = v.sqrMagnitude > 1e-6f ? v.normalized : _baseFireDirection;
            Vector3 moveRef = _shooterMoveHorizontal.sqrMagnitude > 1e-6f
                ? _shooterMoveHorizontal
                : Vector3.Cross(Vector3.up, _baseFireDirection).normalized;
            Vector3 lateral = Vector3.Cross(Vector3.up, moveRef);
            if (lateral.sqrMagnitude < 1e-6f)
                lateral = Vector3.right;
            lateral.Normalize();
            float wobble = Mathf.Sin(_t * Mathf.PI * 2f * _movementDriftWobbleHz) * _movementDriftLateralAmplitude;
            Vector3 perturbed = (dir + lateral * wobble).normalized;
            if (_shooterMoveHorizontal.sqrMagnitude > 1e-6f)
                perturbed = Vector3.Slerp(perturbed, (perturbed + _shooterMoveHorizontal * 0.12f).normalized, Time.deltaTime * 1.8f);
            Vector3 desiredVel = perturbed * _speed;
            _rb.velocity = Vector3.Lerp(v, desiredVel, _movementDriftSlerp * Time.deltaTime);
        }
        else if (_homingTarget != null && _rb != null)
        {
            Vector3 toTarget = _homingTarget.position - transform.position;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                Vector3 desired = toTarget.normalized * _speed;
                _rb.velocity = Vector3.Lerp(_rb.velocity, desired, HomingStrength * Time.deltaTime);
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        GameObject hit = collision.gameObject;
        if (_damagePlayerLikeEnemy)
        {
            var resources = hit.GetComponentInParent<PlayerMechResources>();
            if (resources != null)
                resources.RegisterEnemyProjectileHit(_playerHealthDamage, _playerToughnessDelta);
        }
        else
        {
            // 优先按「实际碰到的 Collider」向上找 EnemyHitFeedback，避免仅依赖 Enemy 标签链漏配时无法扣血。
            var feedback = hit.GetComponentInParent<EnemyHitFeedback>();
            if (feedback != null)
                feedback.ApplyDamage(_enemyDamage);
            else
            {
                GameObject enemy = GetEnemyRoot(hit);
                if (enemy != null)
                    ApplyEnemyHitEffect(enemy, _enemyDamage);
            }
        }

        Destroy(gameObject);
    }

    static GameObject GetEnemyRoot(GameObject hit)
    {
        Transform t = hit.transform;
        while (t != null)
        {
            if (t.CompareTag("Enemy")) return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    static void ApplyEnemyHitEffect(GameObject hitObject, float damage)
    {
        var feedback = hitObject.GetComponentInParent<EnemyHitFeedback>();
        if (feedback != null)
        {
            feedback.ApplyDamage(damage);
            return;
        }

        var renderers = hitObject.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r.material != null)
            {
                var mat = new Material(r.material);
                mat.color = EnemyHitFeedback.HitColor;
                r.material = mat;
            }
        }
        Destroy(hitObject, 1f);
    }
}
