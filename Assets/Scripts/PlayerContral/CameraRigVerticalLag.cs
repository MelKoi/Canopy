using UnityEngine;

/// <summary>
/// 挂在 CameraRig（机甲子物体）上：父物体随机甲上下瞬移时，用 local Y 补偿，使镜头世界高度平滑跟随，减轻空战上下抖动感。
/// </summary>
public class CameraRigVerticalLag : MonoBehaviour
{
    public Transform mechRoot;
    [Tooltip("垂直方向平滑时间（秒），越大镜头高度越“粘滞”")]
    public float verticalSmoothTime = 0.28f;

    float _baseLocalY;
    float _smoothWorldY;
    float _velY;

    void Awake()
    {
        _baseLocalY = transform.localPosition.y;
        if (mechRoot == null)
            mechRoot = transform.parent;
        if (mechRoot != null)
            _smoothWorldY = mechRoot.position.y;
    }

    void LateUpdate()
    {
        if (mechRoot == null)
            return;

        _smoothWorldY = Mathf.SmoothDamp(_smoothWorldY, mechRoot.position.y, ref _velY, verticalSmoothTime, Mathf.Infinity, Time.deltaTime);
        Vector3 lp = transform.localPosition;
        lp.y = _baseLocalY + (_smoothWorldY - mechRoot.position.y);
        transform.localPosition = lp;
    }
}
