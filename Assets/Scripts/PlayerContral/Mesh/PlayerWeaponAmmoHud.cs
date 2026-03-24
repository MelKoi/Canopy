using TMPro;
using UnityEngine;

/// <summary>
/// 右下四角武器弹药 TMP：格式 当前/∞，换弹中显示 reload / ∞。
/// </summary>
public class PlayerWeaponAmmoHud : MonoBehaviour
{
    public WeaponRaycastShooter shooter;

    [Header("可选：留空则按名称在 FightUI 下自动查找")]
    public TMP_Text textLmb;
    public TMP_Text textRmb;
    public TMP_Text textQ;
    public TMP_Text textE;

    const string InfinitySymbol = "\u221e";

    void Awake()
    {
        if (shooter == null)
            shooter = GetComponent<WeaponRaycastShooter>();
        AutoBindTexts();
    }

    void AutoBindTexts()
    {
        if (textLmb != null && textRmb != null && textQ != null && textE != null)
            return;

        foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            string n = tmp.gameObject.name;
            if (n.Contains("RightHandWeapon"))
                textLmb = tmp;
            else if (n.Contains("LeftHandWeapon"))
                textRmb = tmp;
            else if (n.Contains("LeftShoulderWeapon"))
                textQ = tmp;
            else if (n.Contains("RightShoulderWeapon"))
                textE = tmp;
        }
    }

    void LateUpdate()
    {
        if (shooter == null)
            return;

        SetLine(textLmb, 0);
        SetLine(textRmb, 1);
        SetLine(textQ, 2);
        SetLine(textE, 3);
    }

    void SetLine(TMP_Text tmp, int slot)
    {
        if (tmp == null)
            return;

        string cur = shooter.IsReloadingSlot(slot) ? "reload" : shooter.GetMagazineAmmo(slot).ToString();
        tmp.text = $"{cur} / {InfinitySymbol}";
    }
}
