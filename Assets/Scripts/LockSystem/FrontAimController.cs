using UnityEngine;

public class FrontAimController : MonoBehaviour
{
    public LockOnSystem lockOnSystem;
    public float rotateSpeed = 360f; // 每秒最大旋转角度

    void Update()
    {
        if (lockOnSystem == null) return;
        if (lockOnSystem.currentTarget == null) return;

        Vector3 dir = lockOnSystem.currentTarget.position - transform.position;
        dir.y = 0f; // 如果不希望上下点头，可保留

        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            rotateSpeed * Time.deltaTime
        );
    }
}
