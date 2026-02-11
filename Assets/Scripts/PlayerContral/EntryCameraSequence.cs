using System.Collections;
using UnityEngine;

public class EntryCameraSequence : MonoBehaviour//入场动画脚本
{
    public Camera cam;
    public Transform mech;//第一人称相机位置
    public Transform thirdPersonAnchor;//第三人称相机位置

    public float pullBackDuration = 3f;//视角拉回的时间
    public CameraController controller;//获取相机控制

    IEnumerator Start()
    {
        controller.enableInput = false;
        // 黑屏 + 第一人称
        cam.transform.position = mech.position;
        cam.transform.rotation = mech.rotation;

        yield return StartCoroutine(ScreenFadeIn());

        // 拉远到第三人称
        Vector3 startPos = cam.transform.position;
        Quaternion startRot = cam.transform.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / pullBackDuration;
            cam.transform.position = Vector3.Lerp(startPos, thirdPersonAnchor.position, t);
            cam.transform.rotation = Quaternion.Slerp(startRot, thirdPersonAnchor.rotation, t);
            yield return null;
        }
        controller.ResetViewFromCurrentCamera();

        controller.enableInput = true;
    }

    IEnumerator ScreenFadeIn()
    {
        // UI Canvas 黑幕 → 透明
        yield return new WaitForSeconds(0.75f);
    }
}
