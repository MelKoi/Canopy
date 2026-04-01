using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 每次进入 Level_0 时确保：教程用 UI 画布（场景内 <c>Story</c> 优先，否则可选创建 <c>Teaching</c>）、LevelTutorial 存在。
/// 黄昏氛围由场景中手动挂载的 <see cref="TwilightSceneAtmosphere"/> 等负责。
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

        EnsureTeachingCanvas(scene);
        EnsureTutorial(scene);
    }

    static void MoveToScene(GameObject go, Scene scene)
    {
        if (go.scene != scene)
            SceneManager.MoveGameObjectToScene(go, scene);
    }

    static void EnsureTeachingCanvas(Scene scene)
    {
        if (SceneContainsTransformNamed(scene, "Story"))
            return;
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
        // 场景里已经有任何一个 LevelTutorialStep1，就不再动它（支持你手动挂在任意物体上）
        foreach (var t in UnityEngine.Object.FindObjectsByType<LevelTutorialStep1>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.gameObject.scene == scene)
                return;
        }

        // 尝试优先使用你场景中名为 "tutorial" 的物体作为宿主（名称不区分大小写）
        Transform tutorialHost = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            tutorialHost = FindTransformByNameRecursive(root.transform, "tutorial");
            if (tutorialHost != null)
                break;
        }

        GameObject go;
        if (tutorialHost != null)
        {
            go = tutorialHost.gameObject;
        }
        else
        {
            go = new GameObject("LevelTutorial");
        }

        MoveToScene(go, scene);
        if (go.GetComponent<LevelTutorialStep1>() == null)
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
        if (string.Equals(t.name, objectName, StringComparison.OrdinalIgnoreCase))
            return true;
        for (int i = 0; i < t.childCount; i++)
        {
            if (TransformNamedRecursive(t.GetChild(i), objectName))
                return true;
        }
        return false;
    }

    // 简单版本：仅供在本类中查找 Transform（名称不区分大小写）
    static Transform FindTransformByNameRecursive(Transform t, string objectName)
    {
        if (string.Equals(t.name, objectName, StringComparison.OrdinalIgnoreCase))
            return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var found = FindTransformByNameRecursive(t.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }
}
