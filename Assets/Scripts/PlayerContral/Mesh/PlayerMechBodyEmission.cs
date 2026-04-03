using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 机体轮廓光圈：用菲涅尔边缘光（additive）叠在原始网格上，不修改 URP Lit 的全身 Emission，避免「涂白漆」感。
/// 在对应 Renderer 下生成一层复制网格，仅绘制 <see cref="RimShaderName"/>。
/// </summary>
[DisallowMultipleComponent]
public class PlayerMechBodyEmission : MonoBehaviour
{
    public const string RimShaderName = "Custom/CanopyMechRimGlow";

    [Header("边缘光（菲涅尔）")]
    [ColorUsage(true, true)]
    [Tooltip("轮廓颜色（HDR，可略超 1）")]
    public Color emissionColor = new Color(0.35f, 0.55f, 0.85f, 1f);

    [Tooltip("整体强度倍率，保持较小即可见「一圈光」而非整面发亮")]
    [Range(0.01f, 2f)] public float emissionMultiplier = 0.22f;

    [Tooltip("越大边缘越「细、利」，越小越晕开")]
    [Range(0.5f, 16f)] public float rimPower = 5.5f;

    [Tooltip("与 PlayerMechResources 一致，排除 FightUI 等")]
    public Transform uiRootToExclude;

    static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    readonly List<GameObject> _rimRoots = new List<GameObject>();
    Material _rimMaterial;

    void Awake()
    {
        if (uiRootToExclude == null)
        {
            var t = transform.Find("FightUI");
            if (t != null)
                uiRootToExclude = t;
        }

        var sh = Shader.Find(RimShaderName);
        if (sh == null)
        {
            Debug.LogWarning($"[{nameof(PlayerMechBodyEmission)}] 未找到 Shader「{RimShaderName}」，边缘光已跳过。");
            return;
        }

        _rimMaterial = new Material(sh) { name = "Runtime_MechRimGlow" };
        PushMaterialParams();

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (uiRootToExclude != null && r.transform.IsChildOf(uiRootToExclude))
                continue;
            if (r.gameObject.name == "__RimGlow")
                continue;
            switch (r)
            {
                case SkinnedMeshRenderer smr:
                    AddRimSkinnedCopy(smr);
                    break;
                case MeshRenderer mr:
                    AddRimMeshCopy(mr);
                    break;
            }
        }
    }

    void OnDestroy()
    {
        foreach (var go in _rimRoots)
        {
            if (go != null)
                Destroy(go);
        }

        _rimRoots.Clear();
        if (_rimMaterial != null)
            Destroy(_rimMaterial);
    }

    void PushMaterialParams()
    {
        if (_rimMaterial == null)
            return;
        _rimMaterial.SetColor(RimColorId, emissionColor);
        _rimMaterial.SetFloat(RimPowerId, rimPower);
        _rimMaterial.SetFloat(IntensityId, emissionMultiplier);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_rimMaterial != null)
            PushMaterialParams();
    }
#endif

    void AddRimSkinnedCopy(SkinnedMeshRenderer src)
    {
        if (src.sharedMesh == null)
            return;

        var go = new GameObject("__RimGlow");
        go.transform.SetParent(src.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.layer = src.gameObject.layer;

        var dst = go.AddComponent<SkinnedMeshRenderer>();
        dst.sharedMesh = src.sharedMesh;
        dst.sharedMaterials = new[] { _rimMaterial };
        dst.bones = src.bones;
        dst.rootBone = src.rootBone;
        dst.localBounds = src.localBounds;
        dst.quality = src.quality;
        dst.updateWhenOffscreen = src.updateWhenOffscreen;
        dst.skinnedMotionVectors = false;
        dst.lightProbeUsage = LightProbeUsage.Off;
        dst.reflectionProbeUsage = ReflectionProbeUsage.Off;
        dst.shadowCastingMode = ShadowCastingMode.Off;
        dst.receiveShadows = false;
        dst.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        dst.allowOcclusionWhenDynamic = false;
        dst.renderingLayerMask = src.renderingLayerMask;
        dst.sortingOrder = src.sortingOrder;

        _rimRoots.Add(go);
    }

    void AddRimMeshCopy(MeshRenderer src)
    {
        var mf = src.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;

        var go = new GameObject("__RimGlow");
        go.transform.SetParent(src.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.layer = src.gameObject.layer;

        var mfd = go.AddComponent<MeshFilter>();
        mfd.sharedMesh = mf.sharedMesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { _rimMaterial };
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        mr.renderingLayerMask = src.renderingLayerMask;
        mr.sortingOrder = src.sortingOrder;

        _rimRoots.Add(go);
    }
}
