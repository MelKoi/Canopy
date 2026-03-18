using UnityEngine;

/// <summary>
/// 简单球形弹体：高速直线飞行，碰撞后销毁。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ProjectileBullet : MonoBehaviour
{
    float _maxLifetime = 3f;

    public void Setup(float speed, Vector3 direction, Collider[] ignoreColliders, float maxLifetime)
    {
        _maxLifetime = maxLifetime;
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
            Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        Destroy(gameObject);
    }
}
