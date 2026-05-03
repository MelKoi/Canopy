using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Hall 场景：装备按钮切换 EquipePanel；开始游戏加载 Level_0。
/// 打开装备面板时，在指定 UI 矩形内用 RenderTexture 显示玩家 Mesh 预制体预览（铺满预览区）。
/// </summary>
public class HallSceneUIController : MonoBehaviour
{
    [SerializeField] GameObject equipePanel;
    [SerializeField] Button equipeButton;
    [SerializeField] Button startGameButton;
    [SerializeField] string levelSceneName = "Level_0";

    [Header("装备预览")]
    [Tooltip("玩家机甲 Mesh 预制体（与 PlayerRoot 内嵌的 Mesh 一致，如 Assets/Prefeb/Mesh.prefab）")]
    [SerializeField] GameObject mechMeshPrefab;
    [Tooltip("预览参照的 UI RectTransform（机甲渲染将铺满此矩形，一般用方形 Image）")]
    [SerializeField] RectTransform mechPreviewRect;
    [SerializeField] float previewMeshYaw = 200f;
    [SerializeField] float previewCameraPitch = 14f;
    [SerializeField] float previewCameraOrbitYaw = 32f;
    [SerializeField, Range(1f, 1.35f)] float previewFillMargin = 1.06f;

    static readonly Vector3 StagingWorldPos = new Vector3(10000f, 10000f, 0f);

    bool _equipeOpen;
    Canvas _rootCanvas;

    GameObject _previewStagingRoot;
    GameObject _previewModel;
    Camera _previewCamera;
    Light _previewLight;
    RenderTexture _renderTexture;
    RawImage _previewRawImage;
    Vector2Int _lastPreviewPixelSize = new Vector2Int(-1, -1);

    void Awake()
    {
        _rootCanvas = GetComponentInParent<Canvas>();

        if (equipePanel != null)
            equipePanel.SetActive(false);

        if (equipeButton != null)
            equipeButton.onClick.AddListener(ToggleEquipePanel);

        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartLevel);
    }

    void OnDestroy()
    {
        TeardownMechPreview();
        if (equipeButton != null)
            equipeButton.onClick.RemoveListener(ToggleEquipePanel);
        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(StartLevel);
    }

    void LateUpdate()
    {
        if (!_equipeOpen || _renderTexture == null || mechPreviewRect == null)
            return;

        Vector2Int px = GetPreviewPixelSize();
        if (px == _lastPreviewPixelSize)
            return;
        _lastPreviewPixelSize = px;
        ResizeRenderTexture(px.x, px.y);
    }

    void ToggleEquipePanel()
    {
        if (equipePanel == null)
            return;

        _equipeOpen = !_equipeOpen;
        equipePanel.SetActive(_equipeOpen);

        if (_equipeOpen)
            BuildMechPreview();
        else
            TeardownMechPreview();
    }

    void StartLevel()
    {
        if (string.IsNullOrEmpty(levelSceneName))
            return;
        SceneManager.LoadScene(levelSceneName);
    }

    void BuildMechPreview()
    {
        if (mechMeshPrefab == null || mechPreviewRect == null)
            return;

        if (_previewStagingRoot != null)
            return;

        Canvas.ForceUpdateCanvases();

        _previewStagingRoot = new GameObject("HallMechPreviewStaging");
        _previewStagingRoot.transform.position = StagingWorldPos;

        _previewModel = Instantiate(mechMeshPrefab, _previewStagingRoot.transform);
        _previewModel.transform.localRotation = Quaternion.Euler(0f, previewMeshYaw, 0f);
        _previewModel.name = "MeshPreviewInstance";

        StripForPreview(_previewModel);
        AlignModelCenterToStaging();

        var camGo = new GameObject("HallMechPreviewCamera");
        camGo.transform.SetParent(_previewStagingRoot.transform, false);
        _previewCamera = camGo.AddComponent<Camera>();
        _previewCamera.clearFlags = CameraClearFlags.SolidColor;
        _previewCamera.backgroundColor = Color.clear;
        _previewCamera.orthographic = true;
        _previewCamera.nearClipPlane = 0.01f;
        _previewCamera.farClipPlane = 200f;
        _previewCamera.depth = -100f;
        _previewCamera.useOcclusionCulling = false;

        var lightGo = new GameObject("HallMechPreviewLight");
        lightGo.transform.SetParent(_previewStagingRoot.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(55f, previewCameraOrbitYaw + 40f, 0f);
        _previewLight = lightGo.AddComponent<Light>();
        _previewLight.type = LightType.Directional;
        _previewLight.intensity = 1.05f;
        _previewLight.cullingMask = ~0;

        PlaceAndFrameCamera();

        Vector2Int px = GetPreviewPixelSize();
        _lastPreviewPixelSize = px;
        _renderTexture = CreateRenderTexture(px.x, px.y);
        _previewCamera.targetTexture = _renderTexture;

        var rawGo = new GameObject("MechPreviewRaw", typeof(RectTransform));
        rawGo.layer = mechPreviewRect.gameObject.layer;
        rawGo.transform.SetParent(mechPreviewRect, false);
        rawGo.transform.SetAsLastSibling();

        var rawRt = rawGo.GetComponent<RectTransform>();
        rawRt.anchorMin = Vector2.zero;
        rawRt.anchorMax = Vector2.one;
        rawRt.pivot = new Vector2(0.5f, 0.5f);
        rawRt.anchoredPosition = Vector2.zero;
        rawRt.sizeDelta = Vector2.zero;
        rawRt.localScale = Vector3.one;

        _previewRawImage = rawGo.AddComponent<RawImage>();
        _previewRawImage.raycastTarget = false;
        _previewRawImage.texture = _renderTexture;
    }

    void TeardownMechPreview()
    {
        _lastPreviewPixelSize = new Vector2Int(-1, -1);

        if (_previewRawImage != null)
        {
            Destroy(_previewRawImage.gameObject);
            _previewRawImage = null;
        }

        ReleaseRenderTexture();

        if (_previewStagingRoot != null)
        {
            Destroy(_previewStagingRoot);
            _previewStagingRoot = null;
        }

        _previewModel = null;
        _previewCamera = null;
        _previewLight = null;
    }

    void ResizeRenderTexture(int w, int h)
    {
        if (_previewCamera == null)
            return;
        ReleaseRenderTexture();
        _renderTexture = CreateRenderTexture(w, h);
        _previewCamera.targetTexture = _renderTexture;
        if (_previewRawImage != null)
            _previewRawImage.texture = _renderTexture;
    }

    void ReleaseRenderTexture()
    {
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }

    static RenderTexture CreateRenderTexture(int w, int h)
    {
        w = Mathf.Clamp(w, 32, 4096);
        h = Mathf.Clamp(h, 32, 4096);
        var rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();
        return rt;
    }

    Vector2Int GetPreviewPixelSize()
    {
        if (mechPreviewRect == null)
            return new Vector2Int(256, 256);

        float sf = 1f;
        if (_rootCanvas != null)
            sf = Mathf.Max(0.25f, _rootCanvas.scaleFactor);

        int w = Mathf.Max(32, Mathf.RoundToInt(mechPreviewRect.rect.width * sf));
        int h = Mathf.Max(32, Mathf.RoundToInt(mechPreviewRect.rect.height * sf));
        return new Vector2Int(w, h);
    }

    void AlignModelCenterToStaging()
    {
        if (_previewModel == null)
            return;

        Bounds wb = ComputeWorldRendererBounds(_previewModel);
        Vector3 delta = StagingWorldPos - wb.center;
        _previewModel.transform.position += delta;
    }

    void PlaceAndFrameCamera()
    {
        if (_previewCamera == null || _previewModel == null)
            return;

        Bounds wb = ComputeWorldRendererBounds(_previewModel);
        Vector3 center = wb.center;
        float span = Mathf.Max(wb.size.x, wb.size.y, wb.size.z, 0.5f);

        Quaternion orbit =
            Quaternion.Euler(previewCameraPitch, previewMeshYaw + previewCameraOrbitYaw, 0f);
        float dist = span * 1.85f;
        Vector3 camPos = center + orbit * (Vector3.back * dist);
        _previewCamera.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(center - camPos, Vector3.up));

        FitOrthoToBounds(_previewCamera, wb, previewFillMargin);
    }

    static void FitOrthoToBounds(Camera cam, Bounds worldBounds, float margin)
    {
        Matrix4x4 w2c = cam.worldToCameraMatrix;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents;
        for (int ix = -1; ix <= 1; ix += 2)
        {
            for (int iy = -1; iy <= 1; iy += 2)
            {
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    Vector3 p = c + new Vector3(e.x * ix, e.y * iy, e.z * iz);
                    Vector3 v = w2c.MultiplyPoint3x4(p);
                    minX = Mathf.Min(minX, v.x);
                    maxX = Mathf.Max(maxX, v.x);
                    minY = Mathf.Min(minY, v.y);
                    maxY = Mathf.Max(maxY, v.y);
                }
            }
        }

        float halfH = (maxY - minY) * 0.5f * margin;
        float halfW = (maxX - minX) * 0.5f * margin;
        float need = Mathf.Max(halfH, halfW / Mathf.Max(0.001f, cam.aspect));
        cam.orthographicSize = Mathf.Max(0.01f, need);
    }

    static Bounds ComputeWorldRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    static void StripForPreview(GameObject root)
    {
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;

        foreach (var al in root.GetComponentsInChildren<AudioListener>(true))
            al.enabled = false;

        foreach (var b in root.GetComponentsInChildren<Behaviour>(true))
        {
            if (b is Renderer)
                continue;
            b.enabled = false;
        }
    }
}
