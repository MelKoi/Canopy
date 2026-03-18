using UnityEngine;

public class FrontAimController : MonoBehaviour
{
    public LockOnSystem lockOnSystem;
    public float rotateSpeed = 360f; // ????????????

    [Tooltip("?????????Front ???????????®∞¶œ????????????? Pivot ?? DownBody??????????????? Root ??????????????çI???")]
    public Transform aimYawReference;

    void Update()
    {
        if (lockOnSystem != null && lockOnSystem.currentTarget != null)
        {
            Vector3 dir = lockOnSystem.currentTarget.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.01f) return;

            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                rotateSpeed * Time.deltaTime
            );
            return;
        }

        if (aimYawReference == null) return;

        Vector3 fwd = aimYawReference.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) return;

        Quaternion yawRot = Quaternion.LookRotation(fwd.normalized);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            yawRot,
            rotateSpeed * Time.deltaTime
        );
    }
}
