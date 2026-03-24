using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 触发器：玩家进入且已击败 TestEnemyPoint (3) 敌人时加载 Level_0。
/// </summary>
public class EndPointLevelTeleport : MonoBehaviour
{
    public string targetSceneName = "Level_0";

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<MechController>() == null)
            return;
        if (!TestLevelProgress.BossFromPoint3Defeated)
            return;

        if (!string.IsNullOrEmpty(targetSceneName))
            SceneManager.LoadScene(targetSceneName);
    }
}
