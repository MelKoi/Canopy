using UnityEngine;

public class LockOnUI : MonoBehaviour
{
    public Camera cam;
    public RectTransform icon;
    public LockOnSystem lockSystem;

    void Update()
    {
        if (lockSystem.currentTarget == null)
        {
            icon.gameObject.SetActive(false);
            return;
        }

        icon.gameObject.SetActive(true);
        Vector3 screenPos = cam.WorldToScreenPoint(lockSystem.currentTarget.position);
        icon.position = screenPos;
    }
}

