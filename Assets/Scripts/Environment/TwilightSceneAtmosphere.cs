using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 将当前场景切入「昏暗黄昏」氛围：全局曝光/色调、环境光、可选程序天空盒与主光微调。
/// 将本组件挂在任意场景物体上（建议空物体）；复制到其它关卡即可复用。
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class TwilightSceneAtmosphere : MonoBehaviour
{
    [Header("全局后期（URP Volume）")]
    [Tooltip("若指定则使用该 Profile，否则在运行时组装一套黄昏参数")]
    public VolumeProfile volumeProfileOverride;

    [Tooltip("全局 Volume 优先级，高于默认值即可覆盖场景里其它 Volume")]
    [Min(0f)] public float volumePriority = 10f;

    [Header("天空盒")]
    [Tooltip("留空则使用内置 Skybox/Procedural 并根据下方参数调成黄昏；也可指定 Panoramic/Cubemap 等材质")]
    public Material skyboxMaterial;

    [Tooltip("程序天空：大气厚度略增偏落日感")]
    [Min(0f)] public float proceduralAtmosphereThickness = 1.18f;

    public Color proceduralSkyTint = new Color(0.52f, 0.34f, 0.42f, 1f);
    public Color proceduralGroundColor = new Color(0.1f, 0.09f, 0.12f, 1f);

    [Min(0f)] public float proceduralExposure = 0.82f;

    [Header("环境光（ Lighting）")]
    public Color ambientSkyColor = new Color(0.22f, 0.2f, 0.28f, 1f);

    public Color ambientEquatorColor = new Color(0.35f, 0.26f, 0.22f, 1f);
    public Color ambientGroundColor = new Color(0.08f, 0.07f, 0.09f, 1f);

    [Range(0f, 2f)] public float ambientIntensity = 0.48f;

    [Range(0f, 2f)] public float reflectionIntensity = 0.55f;

    [Header("可选：主方向光")]
    public Light directionalLight;

    public bool adjustDirectionalLight = true;

    public Color directionalColor = new Color(1f, 0.78f, 0.58f, 1f);

    [Min(0f)] public float directionalIntensity = 0.45f;

    [Header("自动生成 Volume（无 Profile 覆盖时）")]
    [Tooltip("ColorAdjustments.postExposure，越接近 0 场景越亮；默认比旧版 -0.72 略亮")]
    public float generatedPostExposure = -0.55f;

    Volume _volume;
    bool _profileCreatedRuntime;
    Material _runtimeSkyMaterial;

    void Awake()
    {
        EnsureVolume();
        ApplySkyboxAndAmbient();
        ApplyDirectionalLight();
    }

    void OnDestroy()
    {
        if (_profileCreatedRuntime && _volume != null && _volume.profile != null)
            Destroy(_volume.profile);

        if (_runtimeSkyMaterial != null)
            Destroy(_runtimeSkyMaterial);
    }

    void EnsureVolume()
    {
        _volume = GetComponent<Volume>();
        if (_volume == null)
            _volume = gameObject.AddComponent<Volume>();

        _volume.isGlobal = true;
        _volume.priority = volumePriority;
        _volume.weight = 1f;

        if (volumeProfileOverride != null)
        {
            _volume.profile = volumeProfileOverride;
            return;
        }

        _volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _profileCreatedRuntime = true;
        BuildRuntimeTwilightProfile(_volume.profile);
    }

    void BuildRuntimeTwilightProfile(VolumeProfile profile)
    {
        var colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.overrideState = true;
        colorAdj.postExposure.value = generatedPostExposure;
        colorAdj.colorFilter.overrideState = true;
        colorAdj.colorFilter.value = new Color(0.98f, 0.9f, 0.82f, 1f);
        colorAdj.saturation.overrideState = true;
        colorAdj.saturation.value = -14f;
        colorAdj.contrast.overrideState = true;
        colorAdj.contrast.value = 6f;

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.38f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.45f;
        vignette.color.overrideState = true;
        vignette.color.value = new Color(0.02f, 0.02f, 0.05f, 1f);

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.12f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.85f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.62f;
    }

    void ApplySkyboxAndAmbient()
    {
        Material sky = skyboxMaterial;
        if (sky == null)
        {
            var sh = Shader.Find("Skybox/Procedural");
            if (sh != null)
            {
                _runtimeSkyMaterial = new Material(sh)
                {
                    name = "Runtime_TwilightProceduralSky"
                };
                _runtimeSkyMaterial.SetFloat("_SunDisk", 1f);
                _runtimeSkyMaterial.SetFloat("_SunSize", 0.035f);
                _runtimeSkyMaterial.SetFloat("_SunSizeConvergence", 4.5f);
                _runtimeSkyMaterial.SetFloat("_AtmosphereThickness", proceduralAtmosphereThickness);
                _runtimeSkyMaterial.SetColor("_SkyTint", proceduralSkyTint);
                _runtimeSkyMaterial.SetColor("_GroundColor", proceduralGroundColor);
                _runtimeSkyMaterial.SetFloat("_Exposure", proceduralExposure);
                sky = _runtimeSkyMaterial;
            }
        }

        if (sky != null)
            RenderSettings.skybox = sky;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSkyColor * ambientIntensity;
        RenderSettings.ambientEquatorColor = ambientEquatorColor * ambientIntensity;
        RenderSettings.ambientGroundColor = ambientGroundColor * ambientIntensity;
        RenderSettings.reflectionIntensity = reflectionIntensity;
    }

    void ApplyDirectionalLight()
    {
        TryAutoAssignDirectionalLight();
        if (!adjustDirectionalLight || directionalLight == null)
            return;

        directionalLight.color = directionalColor;
        directionalLight.intensity = directionalIntensity;
    }

    /// <summary>未指定时拾取场景中第一盏平行光。</summary>
    public void TryAutoAssignDirectionalLight()
    {
        if (directionalLight != null)
            return;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l == null || l.type != LightType.Directional)
                continue;
            if (l.gameObject.scene != gameObject.scene)
                continue;
            directionalLight = l;
            return;
        }
    }

    /// <summary>重设天空/环境光/主光（用于动态创建组件后再写序列化字段）。</summary>
    public void ReapplyEnvironmentAndDirectional()
    {
        ApplySkyboxAndAmbient();
        ApplyDirectionalLight();
    }

    /// <summary>同步运行时生成 Volume 的 postExposure 与 <see cref="generatedPostExposure"/>。</summary>
    public void RefreshRuntimeVolumeExposure()
    {
        if (!_profileCreatedRuntime || _volume == null || _volume.profile == null)
            return;
        if (_volume.profile.TryGet(out ColorAdjustments ca))
            ca.postExposure.value = generatedPostExposure;
    }
}
