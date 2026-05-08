using System;
using UnityEngine;

/// <summary>
/// 根据机甲 Mesh 层级（LeftArm/Hand、Shlouder 等）解析挂点，并在挂点下挂载的武器上查找「炮口」「枪口」Transform。
/// 与 <see cref="HallMeshEquipmentBinder"/> 的骨骼命名约定一致。
/// </summary>
public static class MechWeaponMuzzleResolver
{
    /// <summary>
    /// WeaponRaycastShooter 槽位：0=LMB=右手，1=RMB=左手，2=Q=左肩，3=E=右肩。
    /// </summary>
    public static Transform ResolveMountForWeaponRaySlot(Transform meshRoot, int raySlotIndex)
    {
        if (meshRoot == null)
            return null;
        switch (raySlotIndex)
        {
            case 0:
                return ResolveMount(meshRoot, MechMountSlot.RightHand);
            case 1:
                return ResolveMount(meshRoot, MechMountSlot.LeftHand);
            case 2:
                return ResolveMount(meshRoot, MechMountSlot.LeftShoulder);
            case 3:
                return ResolveMount(meshRoot, MechMountSlot.RightShoulder);
            default:
                return null;
        }
    }

    /// <summary>
    /// 在 Hand/Shlouder 下找第一件非「链接」类子物体上的炮口/枪口；无则返回 null。
    /// </summary>
    public static Transform FindMuzzleOnMount(Transform mount)
    {
        if (mount == null)
            return null;
        for (int i = 0; i < mount.childCount; i++)
        {
            var c = mount.GetChild(i);
            if (IsLinkName(c.name))
                continue;
            var muzzle = FindChildDepthFirst(c, "炮口") ?? FindChildDepthFirst(c, "枪口");
            if (muzzle != null)
                return muzzle;
        }

        return null;
    }

    /// <summary>挂点下直接子物体中，包含 <paramref name="descendant"/> 的那件武器根（跳过链接子物体）。</summary>
    public static Transform GetWeaponRootUnderMountForDescendant(Transform mount, Transform descendant)
    {
        if (mount == null || descendant == null)
            return null;
        Transform t = descendant;
        while (t != null && t.parent != mount)
            t = t.parent;
        if (t == null || t.parent != mount)
            return null;
        return IsLinkName(t.name) ? null : t;
    }

    /// <summary>武器根名称包含「火箭筒」时视为火箭筒（含左/右）。</summary>
    public static bool IsRocketLauncherWeapon(Transform weaponRoot)
    {
        if (weaponRoot == null)
            return false;
        string n = weaponRoot.name;
        return !string.IsNullOrEmpty(n) && n.IndexOf("\u706B\u7BAD\u7B52", StringComparison.Ordinal) >= 0;
    }

    /// <summary>武器根名称包含「连发枪」时视为连发枪。</summary>
    public static bool IsBurstRifleWeapon(Transform weaponRoot)
    {
        if (weaponRoot == null)
            return false;
        string n = weaponRoot.name;
        return !string.IsNullOrEmpty(n) && n.IndexOf("\u8FDE\u53D1\u67AA", StringComparison.Ordinal) >= 0;
    }

    /// <summary>武器根名称包含「单发炮」时视为单发炮（含左/右）。</summary>
    public static bool IsSingleShotCannonWeapon(Transform weaponRoot)
    {
        if (weaponRoot == null)
            return false;
        string n = weaponRoot.name;
        return !string.IsNullOrEmpty(n) && n.IndexOf("\u5355\u53D1\u70AE", StringComparison.Ordinal) >= 0;
    }

    /// <summary>武器根名称包含「单发枪」时视为单发枪。</summary>
    public static bool IsSingleShotGunWeapon(Transform weaponRoot)
    {
        if (weaponRoot == null)
            return false;
        string n = weaponRoot.name;
        return !string.IsNullOrEmpty(n) && n.IndexOf("\u5355\u53D1\u67AA", StringComparison.Ordinal) >= 0;
    }

    public static bool MeshRootHasLeftArm(Transform root)
    {
        return root != null && FindChildDepthFirst(root, "LeftArm") != null;
    }

    enum MechMountSlot
    {
        LeftHand,
        RightHand,
        LeftShoulder,
        RightShoulder
    }

    static Transform ResolveMount(Transform meshRoot, MechMountSlot slot)
    {
        Transform leftArm = FindChildDepthFirst(meshRoot, "LeftArm");
        Transform rightArm = FindChildDepthFirst(meshRoot, "RighttArm") ?? FindChildDepthFirst(meshRoot, "RightArm");
        Transform arm = slot == MechMountSlot.LeftHand || slot == MechMountSlot.LeftShoulder ? leftArm : rightArm;
        if (arm == null)
            return null;
        string mountName = slot == MechMountSlot.LeftHand || slot == MechMountSlot.RightHand ? "Hand" : "Shlouder";
        for (int i = 0; i < arm.childCount; i++)
        {
            var t = arm.GetChild(i);
            if (t.name == mountName)
                return t;
        }

        return null;
    }

    static Transform FindChildDepthFirst(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindChildDepthFirst(root.GetChild(i), objectName);
            if (f != null)
                return f;
        }

        return null;
    }

    static bool IsLinkName(string n)
    {
        if (string.IsNullOrEmpty(n))
            return false;
        return n == "链接" || n == "衔接点" || n == "连接处";
    }

    /// <summary>挂点下第一件非链接子物体名称是否包含「火箭筒」（左/右等）。</summary>
    public static bool MountHasRocketLauncherEquipped(Transform mount)
    {
        if (mount == null)
            return false;
        for (int i = 0; i < mount.childCount; i++)
        {
            var c = mount.GetChild(i);
            if (IsLinkName(c.name))
                continue;
            string n = c.name ?? string.Empty;
            return n.IndexOf("火箭筒", StringComparison.Ordinal) >= 0;
        }

        return false;
    }

    /// <summary>炮口/枪口是否属于该挂点下名称含「火箭筒」的武器实例。</summary>
    public static bool IsMuzzleFromRocketLauncherOnMount(Transform muzzle, Transform mount)
    {
        if (muzzle == null || mount == null)
            return false;
        if (!muzzle.IsChildOf(mount))
            return false;
        for (int i = 0; i < mount.childCount; i++)
        {
            var w = mount.GetChild(i);
            if (IsLinkName(w.name))
                continue;
            if (!muzzle.IsChildOf(w))
                continue;
            string n = w.name ?? string.Empty;
            return n.IndexOf("火箭筒", StringComparison.Ordinal) >= 0;
        }

        return false;
    }
}
