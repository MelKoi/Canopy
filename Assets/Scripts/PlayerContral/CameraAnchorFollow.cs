using UnityEngine;

public class CameraAnchorFollow : MonoBehaviour
{
    public Transform mech;

    void LateUpdate()
    {
        transform.position = mech.position;
    }
}

