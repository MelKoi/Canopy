using UnityEngine;

/// <summary>
/// 球形弹体：直线飞行，可选轻微制导；碰撞后销毁；击中敌人时敌人变粉红 1 秒后消失。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ProjectileBullet : MonoBehaviour
{
    float _maxLifetime = 3f;
    float _speed;
    Transform _homingTarget;
    [Tooltip("制导强度，越大子弹越会拐向锁定目标")]
    const float HomingStrength = 4f;

    public void Setup(float speed, Vector3 direction, Collider[] ignoreColliders, float maxLifetime,
        Transform homingTarget = null)
    {
        _maxLifetime = maxLifetime;
        _speed = speed;
        _homingTarget = homingTarget;

        var rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.02f;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.velocity = direction.normalized * speed;

        var col = GetComponent<Collider>();
        if (col != null && ignoreColliders != null)
        {
            foreach (var c in ignoreColliders)
            {
                if (c != null)
                    Physics.IgnoreCollision(col, c, true);
            }
        }
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

        if (_homingTarget != null)
        {
            var rb = GetComponent<Rigidbody>();
            Vector3 toTarget = _homingTarget.position - transform.position;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                Vector3 desired = toTarget.normalized * _speed;
                rb.velocity = Vector3.Lerp(rb.velocity, desired, HomingStrength * Time.deltaTime);
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        GameObject hit = collision.gameObject;
        GameObject enemy = GetEnemyRoot(hit);
        if (enemy != null)
            ApplyEnemyHitEffect(enemy);

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

    static void ApplyEnemyHitEffect(GameObject hitObject)
    {
        var feedback = hitObject.GetComponentInParent<EnemyHitFeedback>();
        if (feedback != null)
        {
            feedback.OnHit();
            return;
        }

        var renderers = hitObject.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r.material != null)
            {
                var mat = new Material(r.material);
                mat.color = new Color(1f, 0.41f, 0.71f); // 粉红色
                r.material = mat;
            }
        }
        Destroy(hitObject, 1f);
    }
}
