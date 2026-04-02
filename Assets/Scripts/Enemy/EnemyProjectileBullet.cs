using UnityEngine;

/// <summary>
/// 敌人子弹：击中玩家机甲造成伤害；带刚体与轨迹可视化。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyProjectileBullet : MonoBehaviour
{
    float _maxLifetime = 6f;
    float _speed;
    Rigidbody _rb;
    int _healthDamage = -1;
    int _toughnessDelta = -1;

    public void Setup(float speed, Vector3 direction, Collider[] ignoreColliders, float maxLifetime,
        int healthDamage = -1, int toughnessDelta = -1)
    {
        _maxLifetime = maxLifetime;
        _speed = speed;
        _healthDamage = healthDamage;
        _toughnessDelta = toughnessDelta;

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
        var resources = collision.gameObject.GetComponentInParent<PlayerMechResources>();
        if (resources != null)
            resources.RegisterEnemyProjectileHit(_healthDamage, _toughnessDelta);

        Destroy(gameObject);
    }
}
