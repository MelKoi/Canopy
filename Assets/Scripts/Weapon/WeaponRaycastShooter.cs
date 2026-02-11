using UnityEngine;

public class WeaponRaycastShooter : MonoBehaviour
{
    public Transform front;

    [Header("Weapon Mounts")]
    public Transform leftGun;
    public Transform rightGun;
    public Transform leftShoulder;
    public Transform rightShoulder;

    public float range = 300f;
    public LayerMask hitMask;

    //public LineRenderer debugLine; // 可选

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
        Vector3 origin = firePoint.position;
        Vector3 dir = front.forward;

        Ray ray = new Ray(origin, dir);
        RaycastHit hit;

        Debug.DrawRay(origin, dir * range, Color.red, 3f);

        if (Physics.Raycast(ray, out hit, range, hitMask))
        {
            Debug.Log("Hit: " + hit.collider.name);

            // 命中反馈（临时）
            CreateHitEffect(hit.point, hit.normal);
        }
    }

    void CreateHitEffect(Vector3 pos, Vector3 normal)
    {
        // 现在可以先留空，或 Debug.DrawRay
        Debug.DrawRay(pos, normal, Color.yellow, 1f);
    }
}
