using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// EquipePanel：根据「左手/右手/左肩/右肩」行下的 TMP_Dropdown，把武器挂到 Mesh 的 Hand / Shlouder。
/// Hand：武器「链接/衔接点」与 Hand 原点对齐；肩：与肩下「链接」对齐。
/// 编辑器下写回 Mesh.prefab（单次改动只保存当前槽位）。枪口/炮口大致对齐 Front 物体所代表的机甲前方（用 Front.forward，不指向 Front 世界坐标）。
/// </summary>
[DisallowMultipleComponent]
public class HallMeshEquipmentBinder : MonoBehaviour
{
    const string PlayerPrefsKey = "Canopy.HallMeshLoadout.v1";

    [Tooltip("与 Hall 预览一致的 Mesh 预制体（Assets/Prefeb/Mesh.prefab）")]
    [SerializeField] GameObject mechMeshPrefab;

    [Serializable]
    public class WeaponPrefabEntry
    {
        public string displayName;
        public GameObject prefab;
    }

    [Tooltip("下拉选项文字 → 预制体；未配置时在编辑器中尝试 Assets/Prefeb/Weapon/{选项名}.prefab")]
    [SerializeField] List<WeaponPrefabEntry> weaponPrefabs = new List<WeaponPrefabEntry>();

    [Header("枪口朝向（机甲前方 = Front 的轴向）")]
    [Tooltip("用 Front.forward 与水平面混合时，保留的竖直分量比例（越小越接近水平面内的「前方」）")]
    [SerializeField, Range(0f, 1f)] float frontDirectionVerticalBlend = 0.35f;
    [Tooltip("水平偏航旋转占「对齐到机甲前方」的比例；小于 1 为不完全对齐")]
    [SerializeField, Range(0.15f, 1f)] float muzzleYawAimStrength = 0.62f;
    [Tooltip("在绕世界 Y 轴偏航上额外加的角度（度）；炮口/枪口资源轴向与射击方向差 180° 时用 180，正常用 0")]
    [SerializeField, Range(-360f, 360f)] float muzzleYawExtraDegrees = 180f;
    [Tooltip("使用 -Front.forward 作为机甲前方（仅当 Front 物体轴向与网格「前」约定相反时勾选）")]
    [SerializeField] bool invertMechForwardFromFront = false;

    [Tooltip("以下预制体从「子物体」生成，应像直接挂在父级下一样继承 Hand/Shlouder 的缩放；用 local 挂载而非 world 保持")]
    [SerializeField] List<string> attachAsLocalChildPrefabNames = new List<string>
    {
        "\u5355\u53D1\u70AE \u5DE6",
        "\u5355\u53D1\u70AE \u53F3"
    };

    [Tooltip("对上方列表中的预制体跳过枪口/炮口绕世界 Y 轴偏航对齐；子物体预制体轴向一般已与美术场景一致，避免叠加 muzzleYawExtraDegrees 等")]
    [SerializeField] bool skipMuzzleYawForLocalChildPrefabs = true;

    [Tooltip("local 挂载列表中的预制体在挂点与对齐完成后，绕武器根局部 X 轴（transform.right）相对连接处再旋转的角度")]
    [SerializeField, Range(-180f, 180f)] float attachAsLocalChildRotateXEuler = 90f;

    enum Slot
    {
        LeftHand,
        RightHand,
        LeftShoulder,
        RightShoulder
    }

    readonly List<SlotRow> _rows = new List<SlotRow>();
    readonly List<(TMP_Dropdown dd, UnityAction<int> handler)> _dropdownHandlers = new List<(TMP_Dropdown, UnityAction<int>)>();

    HallSceneUIController _hall;
    bool _suppressDropdownEvents;

    struct SlotRow
    {
        public Slot Slot;
        public TMP_Dropdown Dropdown;
    }

    void OnEnable()
    {
        _hall = FindFirstObjectByType<HallSceneUIController>();
        CacheRows();
        RegisterListeners(true);
        SyncDropdownsFromMeshState();
        if (EnsureDefaultsIfEmpty())
        {
#if UNITY_EDITOR
            SaveAllSlotsToPrefabEditor();
#endif
            SaveLoadoutPrefs();
            _hall?.RebuildMeshPreviewIfOpen();
        }
    }

    void OnDisable()
    {
        RegisterListeners(false);
    }

    void CacheRows()
    {
        _rows.Clear();
        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in texts)
        {
            string label = tmp.text?.Trim() ?? string.Empty;
            Slot? slot = LabelToSlot(label);
            if (slot == null)
                continue;

            Transform row = FindRowUnderEquipePanel(tmp.transform);
            if (row == null)
                continue;
            TMP_Dropdown dd = row.GetComponentInChildren<TMP_Dropdown>(true);
            if (dd == null)
                continue;
            _rows.Add(new SlotRow { Slot = slot.Value, Dropdown = dd });
        }

        DeduplicateRowsBySlot();
    }

    /// <summary>从 TMP 向上找到 EquipePanel 下的「行」根（本组件挂在 EquipePanel 上，transform 即面板根）。</summary>
    Transform FindRowUnderEquipePanel(Transform tmpTransform)
    {
        if (tmpTransform == null || !tmpTransform.IsChildOf(transform))
            return null;
        Transform t = tmpTransform;
        while (t.parent != null && t.parent != transform)
            t = t.parent;
        return t;
    }

    void DeduplicateRowsBySlot()
    {
        var seen = new HashSet<Slot>();
        var filtered = new List<SlotRow>();
        foreach (var r in _rows)
        {
            if (seen.Add(r.Slot))
                filtered.Add(r);
        }
        _rows.Clear();
        _rows.AddRange(filtered);
    }

    static Slot? LabelToSlot(string label)
    {
        if (label == "左手")
            return Slot.LeftHand;
        if (label == "右手")
            return Slot.RightHand;
        if (label == "左肩")
            return Slot.LeftShoulder;
        if (label == "右肩")
            return Slot.RightShoulder;
        return null;
    }

    void RegisterListeners(bool add)
    {
        if (!add)
        {
            foreach (var pair in _dropdownHandlers)
                pair.dd.onValueChanged.RemoveListener(pair.handler);
            _dropdownHandlers.Clear();
            return;
        }

        _dropdownHandlers.Clear();
        foreach (var r in _rows)
        {
            if (r.Dropdown == null)
                continue;
            Slot captured = r.Slot;
            UnityAction<int> handler = idx => OnDropdownChanged(captured, idx);
            r.Dropdown.onValueChanged.AddListener(handler);
            _dropdownHandlers.Add((r.Dropdown, handler));
        }
    }

    void OnDropdownChanged(Slot slot, int index)
    {
        if (_suppressDropdownEvents)
            return;
        var row = _rows.Find(r => r.Slot == slot);
        if (row.Dropdown == null || index < 0 || index >= row.Dropdown.options.Count)
            return;
        string weaponName = row.Dropdown.options[index].text?.Trim() ?? string.Empty;
        ApplyToPreviewAndPersist(slot, weaponName);
    }

    Transform GetPreviewMeshRoot()
    {
        if (_hall != null && _hall.PreviewMeshInstance != null)
            return _hall.PreviewMeshInstance.transform;
        return null;
    }

    void SyncDropdownsFromMeshState()
    {
        Transform probeRoot = GetPreviewMeshRoot();
        GameObject probe = null;
        if (probeRoot == null && mechMeshPrefab != null)
            probe = Instantiate(mechMeshPrefab);

        Transform root = probeRoot != null ? probeRoot : probe != null ? probe.transform : null;
        if (root == null)
        {
            if (probe != null)
                Destroy(probe);
            return;
        }

        _suppressDropdownEvents = true;
        try
        {
            foreach (var r in _rows)
            {
                Transform mount = ResolveMount(root, r.Slot);
                if (mount == null || r.Dropdown == null)
                    continue;
                string mounted = GetMountedWeaponDisplayName(mount);
                int idx = FindOptionIndex(r.Dropdown, mounted);
                if (idx >= 0)
                    r.Dropdown.SetValueWithoutNotify(idx);
            }
        }
        finally
        {
            _suppressDropdownEvents = false;
        }

        if (probe != null)
            Destroy(probe);
    }

    static int FindOptionIndex(TMP_Dropdown dd, string text)
    {
        if (string.IsNullOrEmpty(text))
            return -1;
        for (int i = 0; i < dd.options.Count; i++)
        {
            if (string.Equals(dd.options[i].text?.Trim(), text, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    bool EnsureDefaultsIfEmpty()
    {
        Transform preview = GetPreviewMeshRoot();
        if (preview == null)
            return false;

        bool any = false;
        foreach (var r in _rows)
        {
            Transform mount = ResolveMount(preview, r.Slot);
            if (mount == null || r.Dropdown == null || r.Dropdown.options.Count == 0)
                continue;
            if (GetMountedWeaponTransform(mount) != null)
                continue;
            int idx = Mathf.Clamp(r.Dropdown.value, 0, r.Dropdown.options.Count - 1);
            string name = r.Dropdown.options[idx].text?.Trim() ?? string.Empty;
            var prefab = ResolvePrefab(name);
            ApplyWeaponToMount(mount, r.Slot, prefab, false, mount.root);
            any = true;
        }

        return any;
    }

    void ApplyToPreviewAndPersist(Slot slot, string weaponName)
    {
        Transform preview = GetPreviewMeshRoot();
        if (preview == null)
            return;
        Transform mount = ResolveMount(preview, slot);
        if (mount == null)
            return;
        var prefab = ResolvePrefab(weaponName);
        ApplyWeaponToMount(mount, slot, prefab, false, mount.root);

#if UNITY_EDITOR
        SaveSingleSlotToPrefabEditor(slot);
#endif
        SaveLoadoutPrefs();
        _hall?.RebuildMeshPreviewIfOpen();
    }

    void SaveLoadoutPrefs()
    {
        var data = new MeshLoadoutData { entries = new List<MeshLoadoutEntry>() };
        Transform preview = GetPreviewMeshRoot();
        if (preview == null)
            return;
        foreach (var r in _rows)
        {
            Transform mount = ResolveMount(preview, r.Slot);
            if (mount == null || r.Dropdown == null)
                continue;
            string name = GetMountedWeaponDisplayName(mount);
            if (string.IsNullOrEmpty(name) && r.Dropdown.value >= 0 && r.Dropdown.value < r.Dropdown.options.Count)
                name = r.Dropdown.options[r.Dropdown.value].text?.Trim() ?? string.Empty;
            data.entries.Add(new MeshLoadoutEntry { slot = r.Slot.ToString(), weapon = name });
        }

        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    [Serializable]
    class MeshLoadoutData
    {
        public List<MeshLoadoutEntry> entries;
    }

    [Serializable]
    class MeshLoadoutEntry
    {
        public string slot;
        public string weapon;
    }

#if UNITY_EDITOR
    void SaveAllSlotsToPrefabEditor()
    {
        if (mechMeshPrefab == null)
            return;
        string path = AssetDatabase.GetAssetPath(mechMeshPrefab);
        if (string.IsNullOrEmpty(path))
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var r in _rows)
            {
                Transform mount = ResolveMount(root.transform, r.Slot);
                if (mount == null || r.Dropdown == null)
                    continue;
                int idx = r.Dropdown.value;
                if (idx < 0 || idx >= r.Dropdown.options.Count)
                    continue;
                string weaponName = r.Dropdown.options[idx].text?.Trim() ?? string.Empty;
                var prefab = ResolvePrefab(weaponName);
                ApplyWeaponToMount(mount, r.Slot, prefab, true, mount.root);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    void SaveSingleSlotToPrefabEditor(Slot slot)
    {
        if (mechMeshPrefab == null)
            return;
        string path = AssetDatabase.GetAssetPath(mechMeshPrefab);
        if (string.IsNullOrEmpty(path))
            return;

        var row = _rows.Find(r => r.Slot == slot);
        if (row.Dropdown == null)
            return;
        int idx = row.Dropdown.value;
        if (idx < 0 || idx >= row.Dropdown.options.Count)
            return;
        string weaponName = row.Dropdown.options[idx].text?.Trim() ?? string.Empty;
        var prefab = ResolvePrefab(weaponName);

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform mount = ResolveMount(root.transform, slot);
            if (mount == null)
                return;
            ApplyWeaponToMount(mount, slot, prefab, true, mount.root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
#endif

    GameObject ResolvePrefab(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
            return null;
        foreach (var e in weaponPrefabs)
        {
            if (e != null && e.prefab != null && string.Equals(e.displayName?.Trim(), displayName, StringComparison.Ordinal))
                return e.prefab;
        }

#if UNITY_EDITOR
        string[] candidates =
        {
            $"Assets/Prefeb/Weapon/{displayName}.prefab",
            $"Assets/Prefeb/Weapon/{displayName.Replace(" ", "")}.prefab"
        };
        foreach (var p in candidates)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null)
                return go;
        }
#endif
        return null;
    }

    static Transform ResolveMount(Transform meshRoot, Slot slot)
    {
        Transform leftArm = FindChildDepthFirst(meshRoot, "LeftArm");
        Transform rightArm = FindChildDepthFirst(meshRoot, "RighttArm") ?? FindChildDepthFirst(meshRoot, "RightArm");
        Transform arm = slot == Slot.LeftHand || slot == Slot.LeftShoulder ? leftArm : rightArm;
        if (arm == null)
            return null;
        string mountName = slot == Slot.LeftHand || slot == Slot.RightHand ? "Hand" : "Shlouder";
        foreach (Transform t in arm)
        {
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

    static Transform FindLinkTransform(Transform root)
    {
        if (root == null)
            return null;
        if (IsLinkName(root.name))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindLinkTransform(root.GetChild(i));
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

    bool PrefabUsesLocalChildAttach(GameObject prefab)
    {
        if (prefab == null)
            return false;
        string n = prefab.name?.Trim() ?? string.Empty;
        foreach (var entry in attachAsLocalChildPrefabNames)
        {
            if (string.IsNullOrEmpty(entry))
                continue;
            if (string.Equals(n, entry.Trim(), StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    static Transform GetMountedWeaponTransform(Transform mount)
    {
        for (int i = 0; i < mount.childCount; i++)
        {
            var c = mount.GetChild(i);
            if (!IsLinkName(c.name))
                return c;
        }
        return null;
    }

    static string GetMountedWeaponDisplayName(Transform mount)
    {
        var w = GetMountedWeaponTransform(mount);
        return w != null ? w.name : null;
    }

    static void ClearMountedWeapons(Transform mount, bool shoulderHasLinkChild, bool destroyImmediate)
    {
        for (int i = mount.childCount - 1; i >= 0; i--)
        {
            var c = mount.GetChild(i);
            if (shoulderHasLinkChild && IsLinkName(c.name))
                continue;
            if (destroyImmediate)
                DestroyImmediate(c.gameObject);
            else
                Destroy(c.gameObject);
        }
    }

    void ApplyWeaponToMount(Transform mount, Slot slot, GameObject prefab, bool destroyImmediate, Transform meshRootForFront)
    {
        if (mount == null)
            return;
        bool shoulder = slot == Slot.LeftShoulder || slot == Slot.RightShoulder;
        ClearMountedWeapons(mount, shoulder, destroyImmediate);
        if (prefab == null)
            return;

        var instance = UnityEngine.Object.Instantiate(prefab);
        instance.name = prefab.name;

        bool localAttach = PrefabUsesLocalChildAttach(prefab);
        if (localAttach)
        {
            instance.transform.SetParent(mount, false);
            instance.transform.localScale = prefab.transform.localScale;
            instance.transform.localRotation = prefab.transform.localRotation;
            instance.transform.localPosition = prefab.transform.localPosition;
        }
        else
        {
            instance.transform.SetParent(mount, true);
        }

        Transform weaponLink = FindLinkTransform(instance.transform);
        if (weaponLink == null)
            weaponLink = instance.transform;

        Transform shoulderLink = null;
        if (shoulder)
        {
            shoulderLink = FindLinkTransform(mount);
            if (shoulderLink == null)
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                return;
            }

            Quaternion rotDelta = shoulderLink.rotation * Quaternion.Inverse(weaponLink.rotation);
            instance.transform.rotation = rotDelta * instance.transform.rotation;
            Vector3 posDelta = shoulderLink.position - weaponLink.position;
            instance.transform.position += posDelta;
        }
        else
        {
            Vector3 posDelta = mount.position - weaponLink.position;
            instance.transform.position += posDelta;
        }

        ApplyMuzzleYawTowardFront(instance, weaponLink, shoulderLink, mount, meshRootForFront, prefab);
        ApplyLocalChildExtraRotateX(instance, weaponLink, shoulderLink, mount, shoulder, prefab);
    }

    void ApplyLocalChildExtraRotateX(
        GameObject instance,
        Transform weaponLink,
        Transform shoulderLinkOrNull,
        Transform mount,
        bool shoulder,
        GameObject prefab)
    {
        if (instance == null || weaponLink == null || prefab == null)
            return;
        if (!PrefabUsesLocalChildAttach(prefab))
            return;
        if (Mathf.Abs(attachAsLocalChildRotateXEuler) < 0.01f)
            return;

        Vector3 pivot = weaponLink.position;
        Vector3 axis = instance.transform.right;
        instance.transform.RotateAround(pivot, axis, attachAsLocalChildRotateXEuler);

        if (shoulder && shoulderLinkOrNull != null)
        {
            Vector3 fix = shoulderLinkOrNull.position - weaponLink.position;
            instance.transform.position += fix;
        }
        else
        {
            Vector3 fix = mount.position - weaponLink.position;
            instance.transform.position += fix;
        }
    }

    void ApplyMuzzleYawTowardFront(
        GameObject weaponInstance,
        Transform weaponLink,
        Transform shoulderLinkOrNull,
        Transform mount,
        Transform meshRootForFront,
        GameObject prefabSource)
    {
        if (weaponInstance == null || meshRootForFront == null)
            return;

        if (skipMuzzleYawForLocalChildPrefabs && prefabSource != null && PrefabUsesLocalChildAttach(prefabSource))
            return;

        Transform front = FindChildDepthFirst(meshRootForFront, "Front");
        Vector3 mechForward = front != null
            ? (invertMechForwardFromFront ? -front.forward : front.forward)
            : meshRootForFront.forward;
        mechForward.y *= frontDirectionVerticalBlend;
        if (mechForward.sqrMagnitude < 1e-8f)
            return;
        mechForward.Normalize();

        Transform muzzle = FindChildDepthFirst(weaponInstance.transform, "枪口")
                           ?? FindChildDepthFirst(weaponInstance.transform, "炮口");
        if (muzzle == null)
            muzzle = weaponInstance.transform;

        Vector3 linkWp = weaponLink.position;

        Vector3 muzzleFwd = muzzle.forward;
        muzzleFwd.y *= frontDirectionVerticalBlend;
        if (muzzleFwd.sqrMagnitude < 1e-8f)
            return;
        muzzleFwd.Normalize();

        float signedAngle = Vector3.SignedAngle(muzzleFwd, mechForward, Vector3.up);
        float apply = signedAngle * muzzleYawAimStrength + muzzleYawExtraDegrees;
        if (Mathf.Abs(apply) < 0.2f)
            return;

        weaponInstance.transform.RotateAround(linkWp, Vector3.up, apply);

        if (shoulderLinkOrNull != null)
        {
            Vector3 fix = shoulderLinkOrNull.position - weaponLink.position;
            weaponInstance.transform.position += fix;
        }
        else
        {
            Vector3 fix = mount.position - weaponLink.position;
            weaponInstance.transform.position += fix;
        }
    }
}
