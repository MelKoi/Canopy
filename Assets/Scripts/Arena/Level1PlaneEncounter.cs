using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Level_1：统计「小兵出生点」下四名小兵被击败后，在「敌人出生点」生成 Plane，并由 <see cref="PlaneDistanceHoverAI"/> 控制距离与漂浮。
/// </summary>
public class Level1PlaneEncounter : MonoBehaviour
{
    [SerializeField] GameObject planePrefab;
    [Min(1)] public int minionsRequired = 4;
    [Tooltip("敌人生成点相对场景根 Arena_Level1 的路径")]
    public string enemySpawnPath = "出生点/敌人出生点";
    [Tooltip("在敌人出生点高度基础上再抬高（米），使机体出现在空中")]
    public float spawnHeightAbovePoint = 12f;

    int _kills;
    bool _spawned;
    readonly List<Action> _unsubs = new List<Action>();

    /// <summary>由 <see cref="Level1ArenaBuilder"/> 在构建关卡时写入预制体引用。</summary>
    public void Configure(GameObject plane)
    {
        planePrefab = plane;
    }

    void Start()
    {
#if UNITY_EDITOR
        if (planePrefab == null)
            planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/Plane.prefab");
#endif
        if (planePrefab == null)
        {
            Debug.LogError("Level1PlaneEncounter: 未指定 planePrefab（请在 Inspector 指定 Plane 预制体，或通过菜单「Canopy/构建 Level1 竞技场布局」重建场景）。");
            return;
        }

        Transform root = transform.root != null ? transform.root : transform;
        Transform enemySpawn = root.Find(enemySpawnPath);
        if (enemySpawn == null)
        {
            Debug.LogWarning($"Level1PlaneEncounter: 未找到路径 \"{enemySpawnPath}\"。");
            return;
        }

        StripExistingPlaneUnderSpawn(enemySpawn);
        RegisterMinionKills(root, enemySpawn);

        if (_unsubs.Count == 0)
            Debug.LogWarning("Level1PlaneEncounter: 未找到任何小兵（出生点下名称含「小兵」且含 EnemyHitFeedback）。");
    }

    void OnDestroy()
    {
        foreach (var u in _unsubs)
        {
            try
            {
                u?.Invoke();
            }
            catch
            {
                // ignored
            }
        }

        _unsubs.Clear();
    }

    static void StripExistingPlaneUnderSpawn(Transform enemySpawn)
    {
        for (int i = enemySpawn.childCount - 1; i >= 0; i--)
        {
            var c = enemySpawn.GetChild(i);
            if (c == null)
                continue;
            if (string.Equals(c.name, "Plane", StringComparison.Ordinal))
                Destroy(c.gameObject);
        }
    }

    void RegisterMinionKills(Transform arenaRoot, Transform enemySpawn)
    {
        var spawnRoot = arenaRoot.Find("出生点");
        if (spawnRoot == null)
            return;

        for (int i = 0; i < spawnRoot.childCount; i++)
        {
            var t = spawnRoot.GetChild(i);
            if (t == null || t == enemySpawn)
                continue;
            if (t.name.IndexOf("小兵", StringComparison.Ordinal) < 0)
                continue;

            var feedbacks = t.GetComponentsInChildren<EnemyHitFeedback>(true);
            foreach (var fb in feedbacks)
            {
                if (fb == null)
                    continue;
                EnemyHitFeedback captured = fb;
                Action handler = null;
                handler = () =>
                {
                    captured.OnFinalHitCommitted -= handler;
                    OnMinionDefeated();
                };
                captured.OnFinalHitCommitted += handler;
                _unsubs.Add(() =>
                {
                    if (captured != null)
                        captured.OnFinalHitCommitted -= handler;
                });
            }
        }
    }

    void OnMinionDefeated()
    {
        if (_spawned)
            return;
        _kills++;
        if (_kills < minionsRequired)
            return;

        SpawnPlane();
    }

    void SpawnPlane()
    {
        _spawned = true;
        Transform root = transform.root != null ? transform.root : transform;
        Transform enemySpawn = root.Find(enemySpawnPath);
        if (enemySpawn == null || planePrefab == null)
            return;

        Vector3 pos = enemySpawn.position + Vector3.up * Mathf.Max(0f, spawnHeightAbovePoint);
        Quaternion rot = enemySpawn.rotation;
        var instance = Instantiate(planePrefab, pos, rot);
        instance.name = planePrefab.name;

        if (instance.GetComponent<PlaneDistanceHoverAI>() == null)
            instance.AddComponent<PlaneDistanceHoverAI>();
        if (instance.GetComponent<PlaneCombat>() == null)
            instance.AddComponent<PlaneCombat>();
    }
}
