using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Test_Level 流程：击败 TestEnemyPoint (3) 上的敌人后允许使用 EndPoint 传送。
/// </summary>
public static class TestLevelProgress
{
    public static bool BossFromPoint3Defeated { get; private set; }

    public static void MarkBossFromPoint3Defeated()
    {
        BossFromPoint3Defeated = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register()
    {
        BossFromPoint3Defeated = false;
        SceneManager.sceneLoaded -= ResetIfTestLevel;
        SceneManager.sceneLoaded += ResetIfTestLevel;
    }

    static void ResetIfTestLevel(Scene scene, LoadSceneMode _)
    {
        if (scene.name == "Test_Level")
            BossFromPoint3Defeated = false;
    }
}
