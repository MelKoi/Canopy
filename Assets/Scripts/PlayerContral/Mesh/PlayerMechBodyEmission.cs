using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 为玩家机体网格提供微弱自发光：对 <b>URP Lit 族</b> 材质实例启用 _EMISSION 并写 _EmissionColor（不污染工程里的共享材质），排除 UI 子层级。
/// </summary>
[DisallowMultipleComponent]
public class PlayerMechBodyEmission : MonoBehaviour
{
    const string UrpLit = "Universal Render Pipeline/Lit";
    const string UrpSimpleLit = "Universal Render Pipeline/Simple Lit";
    const string UrpComplexLit = "Universal Render Pipeline/Complex Lit";

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("自发光")]
    [ColorUsage(true, true)]
    public Color emissionColor = new Color(0.25f, 0.45f, 0.65f, 1f);

    [Tooltip("整体亮度倍率，保持较低即「微弱」发光")]
    [Min(0f)] public float emissionMultiplier = 0.12f;

    [Tooltip("与 PlayerMechResources 一致，排除 FightUI 等")]
    public Transform uiRootToExclude;

    readonly List<Renderer> _targets = new List<Renderer>();

    void Awake()
    {
        if (uiRootToExclude == null)
        {
            var t = transform.Find("FightUI");
            if (t != null)
                uiRootToExclude = t;
        }

        CollectRenderers();
        ApplyEmission();
    }

    void CollectRenderers()
    {
        _targets.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (uiRootToExclude != null && r.transform.IsChildOf(uiRootToExclude))
                continue;
            if (r is not MeshRenderer && r is not SkinnedMeshRenderer)
                continue;
            _targets.Add(r);
        }
    }

    static bool IsUrpLitFamily(Material m)
    {
        if (m == null || m.shader == null)
            return false;
        string n = m.shader.name;
        return n == UrpLit || n == UrpSimpleLit || n == UrpComplexLit;
    }

    void ApplyEmission()
    {
        Color hdr = emissionColor * emissionMultiplier;

        foreach (var r in _targets)
        {
            if (r == null)
                continue;

            var mats = r.materials;
            bool any = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var shared = r.sharedMaterials[i];
                if (shared == null || !IsUrpLitFamily(shared))
                    continue;
                if (!mats[i].HasProperty(EmissionColorId))
                    continue;

                var inst = mats[i];
                inst.EnableKeyword("_EMISSION");
                inst.SetColor(EmissionColorId, hdr);
                inst.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mats[i] = inst;
                any = true;
            }

            if (any)
                r.materials = mats;
        }
    }
}
