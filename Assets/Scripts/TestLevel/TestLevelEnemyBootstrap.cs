using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在场景中所有名称以 TestEnemyPoint 开头的空物体处生成 TestEnemy，并写入 spawn 序号（与名称中括号数字一致）。
/// </summary>
public class TestLevelEnemyBootstrap : MonoBehaviour
{
    public GameObject testEnemyPrefab;

    [Tooltip("在生成点 Transform 基础上额外抬高（米），避免敌人陷进地面或过低")]
    public float spawnHeightOffset = 1.35f;

    void Start()
    {
        if (testEnemyPrefab == null)
        {
            Debug.LogWarning("TestLevelEnemyBootstrap: 未指定 testEnemyPrefab。");
            return;
        }

        var roots = new List<Transform>();
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return;
        var gos = scene.GetRootGameObjects();
        foreach (var go in gos)
        {
            string n = go.name;
            if (n == "TestEnemyPoint" || n.StartsWith("TestEnemyPoint ("))
                roots.Add(go.transform);
        }

        roots.Sort((a, b) => SpawnIndexFromName(a.name).CompareTo(SpawnIndexFromName(b.name)));

        foreach (var pt in roots)
        {
            Vector3 pos = pt.position + Vector3.up * spawnHeightOffset;
            var instance = Instantiate(testEnemyPrefab, pos, pt.rotation);
            var combat = instance.GetComponent<TestEnemyCombat>();
            if (combat != null)
                combat.spawnPointIndex = SpawnIndexFromName(pt.gameObject.name);

            var path = pt.GetComponent<EnemyPatrolPath>();
            var agent = instance.GetComponent<EnemyPatrolAgent>();
            if (path != null && agent != null)
                agent.ApplyFromPath(path);
        }
    }

    static int SpawnIndexFromName(string name)
    {
        if (name == "TestEnemyPoint")
            return 0;
        const string prefix = "TestEnemyPoint (";
        if (!name.StartsWith(prefix) || !name.EndsWith(")"))
            return 0;
        string inner = name.Substring(prefix.Length, name.Length - prefix.Length - 1);
        return int.TryParse(inner, out int v) ? v : 0;
    }
}
