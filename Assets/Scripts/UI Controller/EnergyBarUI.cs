using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 根据 MechController 的当前能量更新体力条 Fill 的填充量。
/// 填充对象为 EnergyBarRoot 下的 Fill（UnityEngine.UI.Image，Type 需为 Filled）。
/// </summary>
public class EnergyBarUI : MonoBehaviour
{
    [Tooltip("提供能量数据的机甲控制器")]
    public MechController mech;
    [Tooltip("用于显示填充的 Image（EnergyBarRoot 下的 Fill），Image Type 需设为 Filled")]
    public Image fillImage;
    [Tooltip("填充变化的平滑速度")]
    public float smoothSpeed = 10f;

    void Update()
    {
        if (mech == null || fillImage == null)
            return;

        float targetAmount = mech.MaxEnergy > 0f
            ? Mathf.Clamp01(mech.CurrentEnergy / mech.MaxEnergy)
            : 0f;

        fillImage.fillAmount = smoothSpeed <= 0f
            ? targetAmount
            : Mathf.MoveTowards(fillImage.fillAmount, targetAmount, smoothSpeed * Time.deltaTime);
    }
}
