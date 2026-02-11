using UnityEngine;

public class WeaponController : MonoBehaviour
{
    [Header("Bullet")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 60f;

    [Header("Weapon Mounts")]
    public Transform leftGun;
    public Transform rightGun;
    public Transform leftShoulder;
    public Transform rightShoulder;

    [Header("Aim Reference")]
    public Transform front; // 你的 Front 空物体

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Fire(leftGun);
        }

        if (Input.GetMouseButtonDown(1))
        {
            Fire(rightGun);
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            Fire(leftShoulder);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            Fire(rightShoulder);
        }
    }

    void Fire(Transform firePoint)
    {
        if (firePoint == null) return;
        GameObject bullet = Instantiate(
            bulletPrefab,
            firePoint.position,
            Quaternion.LookRotation(front.forward)
        );

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        rb.linearVelocity = front.forward * bulletSpeed;
    }
}
