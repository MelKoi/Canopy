using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Third-person orbit camera: rotates around target with offset. Call ResetViewFromCurrentCamera()
/// after entry animation (e.g. lerp to third-person anchor) to sync offset and avoid position jump.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Input State")]
    /// <summary>When false, mouse input is ignored (e.g. during entry sequence).</summary>
    public bool enableInput = true;

    [Header("Target & Params")]
    public Transform target;
    public Vector2 sensitivity = new Vector2(0.12f, 0.12f);
    public Vector2 verticalClamp = new Vector2(-30f, 60f);
    public Vector3 offset = new Vector3(0f, 1f, -11f);

    float _yaw;
    float _pitch;
    bool _hasSyncedFromCamera;

    void LateUpdate()
    {
        if (!_hasSyncedFromCamera) return;

        if (enableInput && Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            _yaw += mouseDelta.x * sensitivity.x;
            _pitch -= mouseDelta.y * sensitivity.y;
            _pitch = Mathf.Clamp(_pitch, verticalClamp.x, verticalClamp.y);
        }

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        if (transform.parent != null)
        {
            transform.localPosition = rotation * offset;
            transform.localRotation = rotation;
        }
        else
        {
            if (target == null) return;
            transform.position = target.position + rotation * offset;
            transform.rotation = rotation;
        }
    }

    /// <summary>Sync from current camera. Uses parent local space when camera has parent (CameraRig) so position is not overwritten when parent follows mech.</summary>
    public void ResetViewFromCurrentCamera()
    {
        if (transform.parent != null)
        {
            offset = transform.localPosition;
            _yaw = transform.localEulerAngles.y;
            float rawPitch = transform.localEulerAngles.x;
            if (rawPitch > 180f) rawPitch -= 360f;
            _pitch = Mathf.Clamp(rawPitch, verticalClamp.x, verticalClamp.y);
        }
        else
        {
            if (target == null) return;
            Vector3 toCam = transform.position - target.position;
            float distance = toCam.magnitude;
            if (distance < 0.001f) return;
            Vector3 dir = toCam / distance;
            _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            _pitch = Mathf.Clamp(_pitch, verticalClamp.x, verticalClamp.y);
            Quaternion invRot = Quaternion.Inverse(Quaternion.Euler(_pitch, _yaw, 0f));
            offset = invRot * toCam;
        }

        _hasSyncedFromCamera = true;
    }

    /// <summary>Set view by explicit yaw and pitch (e.g. from external logic).</summary>
    public void ResetView(float newYaw, float newPitch)
    {
        _yaw = newYaw;
        _pitch = Mathf.Clamp(newPitch, verticalClamp.x, verticalClamp.y);
    }
}
