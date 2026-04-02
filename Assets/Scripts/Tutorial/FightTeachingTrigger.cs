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
    [Tooltip("在吸附地面后的世界 Y 上再抬高（米），一般保持 0")]
    public float spawnHeightOffset;
    [Tooltip("从刷新点向上偏移后再向下打射线找地面")]
    public float groundSnapRayStartUp = 4f;
    [Tooltip("向下射线最大长度")]
    public float groundSnapMaxDistance = 120f;
    [Tooltip("命中地面后，根物体在命中点之上的高度；≤0 时按预制体 CapsuleCollider 与缩放自动算（推荐 0）")]
    public float enemyRootYAboveGround;
    public LayerMask groundSnapMask = ~0;

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
            Vector3 pos = GetSpawnPosition(pt);
            var instance = Instantiate(noMoveEnemyPrefab, pos, pt.rotation);
            var combat = instance.GetComponent<TestEnemyCombat>();
            if (combat != null)
            {
                combat.spawnPointIndex = i;
                combat.reportBossFromPoint3 = false;
            }
        }
    }

    Vector3 GetSpawnPosition(Transform pt)
    {
        Vector3 p = pt.position;
        Vector3 origin = p + Vector3.up * Mathf.Max(0.05f, groundSnapRayStartUp);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundSnapMaxDistance, groundSnapMask,
                QueryTriggerInteraction.Ignore))
        {
            float lift = enemyRootYAboveGround > 0.01f
                ? enemyRootYAboveGround
                : ComputeCapsuleBottomToRootLift(noMoveEnemyPrefab);
            p = hit.point + Vector3.up * lift;
        }

        return p + Vector3.up * spawnHeightOffset;
    }

    static float ComputeCapsuleBottomToRootLift(GameObject prefab)
    {
        if (prefab == null)
            return 1f;
        var cap = prefab.GetComponent<CapsuleCollider>();
        if (cap == null)
            return 1f;
        float sy = Mathf.Abs(prefab.transform.lossyScale.y);
        if (cap.direction != 1)
            return Mathf.Max(0.05f, cap.bounds.extents.y);
        return Mathf.Max(0.05f, cap.center.y * sy + cap.height * 0.5f * sy);
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
