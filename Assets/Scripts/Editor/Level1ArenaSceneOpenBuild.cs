using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在编辑器中打开 Level_1 且场景中尚无竞技场根节点时，自动构建一次（不进入 Play），便于协作与首次打开即见布局。
/// </summary>
[InitializeOnLoad]
static class Level1ArenaSceneOpenBuild
{
    static Level1ArenaSceneOpenBuild()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path != Level1ArenaBuilder.ScenePath)
            return;
        if (FindArenaRootInScene(scene) != null)
            return;

        Level1ArenaBuilder.BuildInternal();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static GameObject FindArenaRootInScene(Scene scene)
    {
        if (!scene.IsValid())
            return null;
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == "Arena_Level1")
                return go;
        }

        return null;
    }
}
