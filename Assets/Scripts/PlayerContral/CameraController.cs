using UnityEngine;
using UnityEngine.InputSystem;   // ★ 新输入系统命名空间

public class CameraController : MonoBehaviour//镜头移动的脚本
{
    public bool enableInput = true;//用于解决同开场效果冲突的判定变量，表示是否允许鼠标输入

    public Transform target;

    public Vector2 sensitivity = new Vector2(0.12f, 0.12f);
    public Vector2 verticalClamp = new Vector2(-30f, 60f);
    public Vector3 offset = new Vector3(0, 1, -11);

    private float yaw;
    private float pitch;

    void LateUpdate()
    {
        if (!enableInput) return;
        if (Mouse.current == null) return;
        // 读取鼠标每帧的移动增量
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * sensitivity.x;
        pitch -= mouseDelta.y * sensitivity.y;

        pitch = Mathf.Clamp(pitch, verticalClamp.x, verticalClamp.y);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = target.position + rotation * offset;
        transform.rotation = rotation;
    }
    public void ResetViewFromCurrentCamera()//视角重置接口
    {
        Vector3 euler = transform.rotation.eulerAngles;

        yaw = euler.y;

        float rawPitch = euler.x;
        if (rawPitch > 180f) rawPitch -= 360f;
        pitch = Mathf.Clamp(rawPitch, verticalClamp.x, verticalClamp.y); ;
    }
    public void ResetView(float newYaw, float newPitch)//可从外部制定角度进行视角变换
    {
        yaw = newYaw;
        pitch = Mathf.Clamp(newPitch, verticalClamp.x, verticalClamp.y);
    }

}
