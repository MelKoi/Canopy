using UnityEngine;

/// <summary>
/// 统一开关玩家可操作输入（移动/视角/射击/锁定）。教学或演出期间可保持锁定。
/// </summary>
public class PlayerGameplayInputGate : MonoBehaviour
{
    [SerializeField] bool lockedOnStart = true;

    MechInput _mechInput;
    CameraController _cameraController;
    WeaponRaycastShooter _weaponShooter;
    LockOnSystem _lockOnSystem;

    bool _locked;

    public bool IsLocked => _locked;

    void Awake()
    {
        CacheReferences();
        _locked = lockedOnStart;
        ApplyLockState();
    }

    void CacheReferences()
    {
        _mechInput = GetComponent<MechInput>();
        Transform root = transform;
        while (root.parent != null)
            root = root.parent;

        _cameraController = root.GetComponentInChildren<CameraController>(true);
        _weaponShooter = root.GetComponentInChildren<WeaponRaycastShooter>(true);
        _lockOnSystem = root.GetComponentInChildren<LockOnSystem>(true);
    }

    public void SetLocked(bool locked)
    {
        _locked = locked;
        ApplyLockState();
    }

    void ApplyLockState()
    {
        bool allow = !_locked;

        if (_mechInput != null)
            _mechInput.GameplayInputEnabled = allow;

        if (_cameraController != null)
            _cameraController.enableInput = allow;

        if (_weaponShooter != null)
            _weaponShooter.enabled = allow;

        if (_lockOnSystem != null)
            _lockOnSystem.enabled = allow;
    }

    /// <summary>场景中查找门控；若无则挂在 <see cref="MechInput"/> 所在物体上并创建。</summary>
    public static PlayerGameplayInputGate FindOrCreate()
    {
        var existing = FindFirstObjectByType<PlayerGameplayInputGate>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        var mechInput = FindFirstObjectByType<MechInput>(FindObjectsInactive.Include);
        if (mechInput == null)
            return null;

        return mechInput.gameObject.AddComponent<PlayerGameplayInputGate>();
    }
}
