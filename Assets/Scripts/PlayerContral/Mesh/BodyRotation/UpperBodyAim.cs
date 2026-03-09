using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpperBodyAim : MonoBehaviour
{
    public Transform cameraPivot;

    void Update()
    {
        float pitch = cameraPivot.eulerAngles.x;

        if (pitch > 180) pitch -= 360;

        transform.localRotation = Quaternion.Euler(pitch, 0, 0);
    }
}
