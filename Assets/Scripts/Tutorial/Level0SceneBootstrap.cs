using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
/// <summary>
/// 每次进入 Level_0 时确保：TwilightAtmosphere、Teaching 画布、LevelTutorial 存在。
/// 解决首包场景不是 Level_0 或后续异步加载时，仅 AfterSceneLoad 执行一次导致教程与氛围未创建的问题。
/// </summary>
static class Level0SceneBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Level_0")
            return;

        EnsureTwilightAtmosphere(scene);
        EnsureTeachingCanvas(scene);
        EnsureTutorial(scene);
    }

    static void MoveToScene(GameObject go, Scene scene)
    {
        if (go.scene != scene)
            SceneManager.MoveGameObjectToScene(go, scene);
    }

    static void EnsureTwilightAtmosphere(Scene scene)
    {
        TwilightSceneAtmosphere existing = null;
        foreach (var a in Object.FindObjectsByType<TwilightSceneAtmosphere>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (a.gameObject.scene == scene)
            {
                existing = a;
                break;
            }
        }

        if (existing != null)
        {
            existing.TryAutoAssignDirectionalLight();
            existing.ReapplyEnvironmentAndDirectional();
            existing.RefreshRuntimeVolumeExposure();
            return;
        }

        var go = new GameObject("TwilightAtmosphere");
        MoveToScene(go, scene);
        var tw = go.AddComponent<TwilightSceneAtmosphere>();
        tw.proceduralExposure = 0.95f;
        tw.ambientIntensity = 0.64f;
        tw.reflectionIntensity = 0.58f;
        tw.generatedPostExposure = -0.48f;
        tw.TryAutoAssignDirectionalLight();
        tw.ReapplyEnvironmentAndDirectional();
        tw.RefreshRuntimeVolumeExposure();
    }

    static void EnsureTeachingCanvas(Scene scene)
    {
        if (SceneContainsTransformNamed(scene, "Teaching"))
            return;

        var teaching = new GameObject("Teaching");
        MoveToScene(teaching, scene);
        var canvas = teaching.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        var scaler = teaching.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        teaching.AddComponent<GraphicRaycaster>();
    }

    static void EnsureTutorial(Scene scene)
    {
        foreach (var t in Object.FindObjectsByType<LevelTutorialStep1>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.gameObject.scene == scene)
                return;
        }

        var go = new GameObject("LevelTutorial");
        MoveToScene(go, scene);
        go.AddComponent<LevelTutorialStep1>();
    }

    static bool SceneContainsTransformNamed(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (TransformNamedRecursive(root.transform, objectName))
                return true;
        }
        return false;
    }

    static bool TransformNamedRecursive(Transform t, string objectName)
    {
        if (t.name == objectName)
            return true;
        for (int i = 0; i < t.childCount; i++)
        {
            if (TransformNamedRecursive(t.GetChild(i), objectName))
                return true;
        }
        return false;
    }
}
