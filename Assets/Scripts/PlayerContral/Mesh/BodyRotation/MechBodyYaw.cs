using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MechBodyYaw : MonoBehaviour
{
    public Transform cameraPivot;
    public float rotateSpeed = 180f;

    void Update()
    {
        Vector3 cameraForward = cameraPivot.forward;
        cameraForward.y = 0;

        Quaternion targetRotation = Quaternion.LookRotation(cameraForward);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }
}
