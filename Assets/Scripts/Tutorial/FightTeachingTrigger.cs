using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 挂在 <c>FightTeaching</c> 触发器上：玩家机甲进入后刷出教学用静止敌人，并触发叙事一句。
/// 刷新点取场景内 <c>battleplace1</c> 下名称符合 EnemyPoint / enemypoint 的子物体。
/// </summary>
[RequireComponent(typeof(Collider))]
public class FightTeachingTrigger : MonoBehaviour
{
    public GameObject noMoveEnemyPrefab;
    public LevelTutorialStep1 tutorial;
    [Tooltip("在生成点本地 Y 上额外抬高（米）")]
    public float spawnHeightOffset = 1.35f;

    bool _done;

    void Awake()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
        if (tutorial == null)
            tutorial = FindFirstObjectByType<LevelTutorialStep1>(FindObjectsInactive.Include);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_done)
            return;
        if (other.GetComponentInParent<MechController>() == null)
            return;
        if (noMoveEnemyPrefab == null)
        {
            Debug.LogWarning("FightTeachingTrigger: 未指定 noMoveEnemyPrefab。");
            return;
        }

        _done = true;
        SpawnEnemies();
        if (tutorial != null)
            tutorial.NotifyFightTeachingEntered();
    }

    void SpawnEnemies()
    {
        Transform battle = FindNamedTransformInLoadedScenes("battleplace1");
        if (battle == null)
        {
            Debug.LogWarning("FightTeachingTrigger: 场景中未找到 battleplace1。");
            return;
        }

        var points = CollectSpawnPoints(battle);
        if (points.Count == 0)
        {
            Debug.LogWarning("FightTeachingTrigger: battleplace1 下没有可用的 EnemyPoint / enemypoint。");
            return;
        }

        for (int i = 0; i < points.Count; i++)
        {
            Transform pt = points[i];
            Vector3 pos = pt.position + Vector3.up * spawnHeightOffset;
            var instance = Instantiate(noMoveEnemyPrefab, pos, pt.rotation);
            var combat = instance.GetComponent<TestEnemyCombat>();
            if (combat != null)
            {
                combat.spawnPointIndex = i;
                combat.reportBossFromPoint3 = false;
            }
        }
    }

    static List<Transform> CollectSpawnPoints(Transform battleRoot)
    {
        var list = new List<Transform>();
        for (int i = 0; i < battleRoot.childCount; i++)
        {
            Transform c = battleRoot.GetChild(i);
            if (IsEnemySpawnPointName(c.name))
                list.Add(c);
        }

        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }

    static bool IsEnemySpawnPointName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        string n = name.Trim();
        if (string.Equals(n, "enemypoint", System.StringComparison.OrdinalIgnoreCase))
            return true;
        return n.StartsWith("EnemyPoint", System.StringComparison.OrdinalIgnoreCase);
    }

    static Transform FindNamedTransformInLoadedScenes(string objectName)
    {
        for (int si = 0; si < SceneManager.sceneCount; si++)
        {
            Scene s = SceneManager.GetSceneAt(si);
            if (!s.isLoaded || !s.IsValid())
                continue;
            foreach (GameObject root in s.GetRootGameObjects())
            {
                Transform found = FindTransformByNameRecursive(root.transform, objectName);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    static Transform FindTransformByNameRecursive(Transform t, string objectName)
    {
        if (string.Equals(t.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform found = FindTransformByNameRecursive(t.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
