using UnityEngine;

public class MechAimController : MonoBehaviour
{
    public Transform cameraTransform;
    public Transform mechMesh;   // Mesh 或 Front 的父级
    public float rotateSpeed = 720f;

    void Update()
    {
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(forward);
        mechMesh.rotation = Quaternion.RotateTowards(
            mechMesh.rotation,
            targetRot,
            rotateSpeed * Time.deltaTime
        );
    }
}
