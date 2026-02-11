using UnityEngine;
using UnityEngine.UI;

public class EnergyBarUI : MonoBehaviour
{
    [Header("Reference")]
    public MechController mech;//机体
    public Image fillImage;//填充图片

    [Header("Smooth")]
    public float smoothSpeed = 10f;//平滑进度条变动速度

    float currentFill = 1f;//填充图片的长
   

    void Update()
    {
        if (mech == null || fillImage == null) return;

        float targetFill = mech.CurrentEnergy / mech.MaxEnergy;//计算当前的位置

        currentFill = Mathf.Lerp(
            currentFill,
            targetFill,
            Time.deltaTime * smoothSpeed
        );//获取具体数值

        fillImage.fillAmount = currentFill;//调整
        //if (targetFill < 0.25f)
        //    fillImage.color = Color.red;
        //else
        //    fillImage.color = Color.aliceBlue;

    }
}
