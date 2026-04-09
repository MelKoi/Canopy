using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Level_1 竞技场：按机甲 Mesh 高度定比例，区分材质；高桥洞供机甲穿行；出生点下放置关卡预制体。
/// 菜单：Canopy / 构建 Level1 竞技场布局
/// </summary>
public static class Level1ArenaBuilder
{
    public const string ScenePath = "Assets/Scenes/Level_1.unity";

    const string MeshPrefabPath = "Assets/Prefeb/Mesh.prefab";
    const string PlayerRootPath = "Assets/Prefeb/PlayerRoot.prefab";
    const string EnemyPlanePath = "Assets/Prefeb/Plane.prefab";
    const string NoMoveEnemyPath = "Assets/Prefeb/noMoveEnemy.prefab";

    const string MatRock = "Assets/Material/Rock.mat";
    const string MatIron = "Assets/Material/Iron.mat";
    const string MatBrick = "Assets/Material/Brick.mat";
    const string MatContainer = "Assets/Material/Container.mat";
    const string MatLightOrange = "Assets/Material/LightOrange.mat";
    const string MatGlass = "Assets/Material/Glass.mat";
    const string MatEnemyAs = "Assets/Material/EnemyAS.mat";

    /// <summary>设计基准机甲高度（米）；场景尺寸按 Mesh 实际高度与此的比值缩放。</summary>
    const float DesignTimeMechHeight = 5.5f;

    /// <summary>
    /// 在机甲比例算出的尺寸上再乘一遍：放大场地半径、出生点外圈、桥距、掩体间距等（桥下净高仍以真实机甲高度为准）。
    /// </summary>
    const float ArenaExtentMultiplier = 10f;

    [MenuItem("Canopy/构建 Level1 竞技场布局")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(ScenePath);
        BuildInternal();
    }

    public static void BuildFromBatch()
    {
        EditorSceneManager.OpenScene(ScenePath);
        BuildInternal();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    internal static void BuildInternal()
    {
        var existing = GameObject.Find("Arena_Level1");
        if (existing != null)
            Object.DestroyImmediate(existing);

        float mechHeight = ResolveReferenceMechHeight();
        float arenaScale = Mathf.Max(0.35f, mechHeight / DesignTimeMechHeight) * ArenaExtentMultiplier;
        float clearanceUnderBridge = mechHeight * 1.08f;
        float deckThickness = 0.5f * arenaScale;
        float bridgeCenterY = clearanceUnderBridge + deckThickness * 0.5f + 0.05f * arenaScale;

        var matFloor = LoadMat(MatRock);
        var matBridge = LoadMat(MatIron);
        var matCover1 = LoadMat(MatBrick);
        var matCover2 = LoadMat(MatContainer);
        var matCover3 = LoadMat(MatLightOrange);
        var matCover4A = LoadMat(MatIron);
        var matCover4B = LoadMat(MatRock);
        var matGlass = LoadMat(MatGlass);
        var matWallAccent = LoadMat(MatEnemyAs);

        var prefabPlayer = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRootPath);
        var prefabEnemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPlanePath);
        var prefabMinion = AssetDatabase.LoadAssetAtPath<GameObject>(NoMoveEnemyPath);

        var root = new GameObject("Arena_Level1");
        Undo.RegisterCreatedObjectUndo(root, "Create Arena_Level1");

        // --- 场地：压扁圆柱仅作 MeshRenderer（无任何 Collider）。若用圆柱体自带 Collider，压扁后体积与机甲多层 Collider 易深度重叠而被挤飞；地面行走只依赖下方极薄盒体。
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.name = "场地地面";
        floor.transform.SetParent(root.transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.15f * arenaScale, 0f);
        floor.transform.localScale = new Vector3(44f * arenaScale, 0.15f * arenaScale, 44f * arenaScale);
        AssignMaterial(floor, matFloor);
        Undo.RegisterCreatedObjectUndo(floor, "Arena floor");

        var floorHit = new GameObject("场地地面碰撞");
        floorHit.transform.SetParent(root.transform, false);
        floorHit.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        floorHit.transform.localScale = Vector3.one;
        var floorBox = floorHit.AddComponent<BoxCollider>();
        float arenaRadius = 22f * arenaScale;
        float floorHitThickness = Mathf.Clamp(0.12f + mechHeight * 0.04f, 0.15f, 0.55f);
        float floorHitSpan = arenaRadius * 2f * 1.22f;
        floorBox.size = new Vector3(floorHitSpan, floorHitThickness, floorHitSpan);
        floorBox.center = new Vector3(0f, -floorHitThickness * 0.5f, 0f);
        Undo.RegisterCreatedObjectUndo(floorHit, "Arena floor hit");
        ApplyWalkableGroundLayer(floorHit);

        // --- 高桥（下方净高供机甲通过）
        CreateBridge(root.transform, "桥2", new Vector3(0f, bridgeCenterY, 5.5f * arenaScale),
            new Vector3(40f * arenaScale, deckThickness, 4f * arenaScale), matBridge);
        CreateBridge(root.transform, "桥1", new Vector3(0f, bridgeCenterY, -5.5f * arenaScale),
            new Vector3(40f * arenaScale, deckThickness, 4f * arenaScale), matBridge);

        // --- 掩体
        CreateCoverBox(root.transform, "掩体1", new Vector3(10f, 0.75f, 12f) * arenaScale, new Vector3(8f, 1.5f, 5f) * arenaScale, matCover1);
        CreateCoverBox(root.transform, "掩体2", new Vector3(-12f, 0.5f, 10f) * arenaScale, new Vector3(4f, 1f, 4f) * arenaScale, matCover2);
        CreateCoverBox(root.transform, "掩体3", new Vector3(12f, 0.5f, -10f) * arenaScale, new Vector3(5f, 1f, 4f) * arenaScale, matCover3);
        CreateCoverCylinder(root.transform, "掩体4_A", new Vector3(-12f, 0.75f, -10f) * arenaScale,
            new Vector3(2.5f, 1.5f, 2.5f) * arenaScale, matCover4A);
        CreateCoverCylinder(root.transform, "掩体4_B", new Vector3(-8.5f, 0.75f, -12.5f) * arenaScale,
            new Vector3(2.5f, 1.5f, 2.5f) * arenaScale, matCover4B);

        // --- 空气墙
        BuildAirWall(root.transform, matGlass, matWallAccent, arenaScale, mechHeight);

        // --- 出生点标记 + 预制体
        var spawnRoot = new GameObject("出生点");
        spawnRoot.transform.SetParent(root.transform, false);
        Undo.RegisterCreatedObjectUndo(spawnRoot, "Spawn root");

        var pPlayer = CreateSpawn(spawnRoot.transform, "玩家出生点", new Vector3(0f, 0f, -20.5f) * arenaScale,
            arenaScale, new Color(0.2f, 0.85f, 1f), aimTowardCenter: false);
        var pEnemy = CreateSpawn(spawnRoot.transform, "敌人出生点", new Vector3(0f, 0f, 20.5f) * arenaScale,
            arenaScale, new Color(1f, 0.35f, 0.35f), aimTowardCenter: true);
        var pMinion1 = CreateSpawn(spawnRoot.transform, "小兵出生点_左上",
            ClampToArenaDiscXZ(new Vector3(-16f, 0f, 14f) * arenaScale, arenaScale),
            arenaScale, new Color(1f, 0.92f, 0.2f), aimTowardCenter: true);
        var pMinion2 = CreateSpawn(spawnRoot.transform, "小兵出生点_右上_掩体上侧",
            ClampToArenaDiscXZ(new Vector3(14f, 0f, 16f) * arenaScale, arenaScale),
            arenaScale, new Color(1f, 0.92f, 0.2f), aimTowardCenter: true);
        var pMinion3 = CreateSpawn(spawnRoot.transform, "小兵出生点_右缘A",
            ClampToArenaDiscXZ(new Vector3(20f, 0f, 8f) * arenaScale, arenaScale),
            arenaScale, new Color(1f, 0.92f, 0.2f), aimTowardCenter: true);
        var pMinion4 = CreateSpawn(spawnRoot.transform, "小兵出生点_右缘B",
            ClampToArenaDiscXZ(new Vector3(20f, 0f, 12f) * arenaScale, arenaScale),
            arenaScale, new Color(1f, 0.92f, 0.2f), aimTowardCenter: true);

        GameObject playerInstance = null;
        if (prefabPlayer != null)
        {
            playerInstance = InstantiateUnitUnderReturn(pPlayer, prefabPlayer, placeOnGround: false);
            if (playerInstance != null)
            {
                AlignPlayerMechBodyToSpawnFacingEnemy(playerInstance, pPlayer, pEnemy);
                PlaceFeetOnGround(playerInstance.transform);
            }
        }
        if (prefabEnemy != null)
            InstantiateUnitUnder(pEnemy, prefabEnemy);
        if (prefabMinion != null)
        {
            InstantiateUnitUnder(pMinion1, prefabMinion);
            InstantiateUnitUnder(pMinion2, prefabMinion);
            InstantiateUnitUnder(pMinion3, prefabMinion);
            InstantiateUnitUnder(pMinion4, prefabMinion);
        }
        else
            Debug.LogWarning($"[Level1ArenaBuilder] 未找到预制体: {NoMoveEnemyPath}");

        if (prefabPlayer == null)
            Debug.LogWarning($"[Level1ArenaBuilder] 未找到预制体: {PlayerRootPath}");
        if (prefabEnemy == null)
            Debug.LogWarning($"[Level1ArenaBuilder] 未找到预制体: {EnemyPlanePath}");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static Material LoadMat(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    /// <summary>圆柱场地半径 = 22 * arenaScale；出生点缩进内圈避免落在圆外。</summary>
    static Vector3 ClampToArenaDiscXZ(Vector3 p, float arenaScale)
    {
        float maxDist = 22f * arenaScale * 0.88f;
        p.y = 0f;
        float d = new Vector2(p.x, p.z).magnitude;
        if (d > maxDist && d > 0.0001f)
        {
            float t = maxDist / d;
            p.x *= t;
            p.z *= t;
        }

        return p;
    }

    static void ApplyWalkableGroundLayer(GameObject go)
    {
        int layer = LayerMask.NameToLayer("Ground");
        if (layer < 0)
            return;
        go.layer = layer;
    }

    /// <summary>以 Mesh 预制体渲染包围盒高度为基准；失败则退回 PlayerRoot；再失败则用设计高度。</summary>
    static float ResolveReferenceMechHeight()
    {
        if (TryPrefabRenderableBounds(MeshPrefabPath, out var bMesh) && bMesh.size.y > 0.25f)
            return bMesh.size.y;

        if (TryPrefabRenderableBounds(PlayerRootPath, out var bPlayer) && bPlayer.size.y > 0.25f)
            return bPlayer.size.y;

        return DesignTimeMechHeight;
    }

    static bool TryPrefabRenderableBounds(string prefabPath, out Bounds bounds)
    {
        bounds = default;
        if (string.IsNullOrEmpty(prefabPath) || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            return false;

        var temp = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            return TryComputeRenderableBounds(temp, out bounds);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(temp);
        }
    }

    static bool TryComputeRenderableBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        bool init = false;
        foreach (var r in renderers)
        {
            if (r.gameObject.GetComponentInParent<Canvas>(true) != null)
                continue;
            if (!init)
            {
                bounds = r.bounds;
                init = true;
            }
            else
                bounds.Encapsulate(r.bounds);
        }

        return init && bounds.size.y > 0.05f;
    }

    static void CreateBridge(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        AssignMaterial(go, mat);
        ApplyWalkableGroundLayer(go);
        Undo.RegisterCreatedObjectUndo(go, name);
    }

    static void CreateCoverBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        AssignMaterial(go, mat);
        ApplyWalkableGroundLayer(go);
        Undo.RegisterCreatedObjectUndo(go, name);
    }

    static void CreateCoverCylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        AssignMaterial(go, mat);
        ApplyWalkableGroundLayer(go);
        Undo.RegisterCreatedObjectUndo(go, name);
    }

    static void AssignMaterial(GameObject go, Material mat)
    {
        if (mat == null) return;
        var r = go.GetComponent<Renderer>();
        if (r != null)
            r.sharedMaterial = mat;
    }

    static void BuildAirWall(Transform parent, Material matGlass, Material matAccent, float arenaScale, float mechHeight)
    {
        var wallRoot = new GameObject("空气墙");
        wallRoot.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(wallRoot, "Air wall root");

        const int segments = 28;
        float wallRadius = 23.2f * arenaScale;
        float wallHeight = Mathf.Max(3.5f * arenaScale, mechHeight * 1.15f);
        float wallThickness = 0.45f * arenaScale;
        float arc = 2f * Mathf.PI * wallRadius / segments;
        float y = wallHeight * 0.5f;

        for (int i = 0; i < segments; i++)
        {
            float ang = (i / (float)segments) * Mathf.PI * 2f;
            float x = wallRadius * Mathf.Sin(ang);
            float z = wallRadius * Mathf.Cos(ang);
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = $"空气墙段_{i:00}";
            seg.transform.SetParent(wallRoot.transform, false);
            seg.transform.localPosition = new Vector3(x, y, z);
            seg.transform.localScale = new Vector3(arc, wallHeight, wallThickness);
            seg.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
            var mat = (i % 7 == 0 && matAccent != null) ? matAccent : matGlass;
            AssignMaterial(seg, mat != null ? mat : matGlass);
            Undo.RegisterCreatedObjectUndo(seg, seg.name);
        }
    }

    static Transform CreateSpawn(Transform parent, string name, Vector3 pos, float arenaScale, Color gizmoColor,
        bool aimTowardCenter)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        if (aimTowardCenter)
            AimTransformTowardWorldPoint(go.transform, Vector3.zero);
        Undo.RegisterCreatedObjectUndo(go, name);
        var marker = go.AddComponent<ArenaSpawnMarker>();
        marker.gizmoColor = gizmoColor;
        marker.radius = Mathf.Max(0.5f, 0.75f * arenaScale);
        return go.transform;
    }

    static void AimTransformTowardWorldPoint(Transform t, Vector3 worldTarget)
    {
        var p = t.position;
        var flat = worldTarget - p;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f)
            return;
        t.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
    }

    static void InstantiateUnitUnder(Transform spawn, GameObject prefab)
    {
        InstantiateUnitUnderReturn(spawn, prefab);
    }

    static GameObject InstantiateUnitUnderReturn(Transform spawn, GameObject prefab, bool placeOnGround = true)
    {
        if (prefab == null || spawn == null)
            return null;
        var inst = PrefabUtility.InstantiatePrefab(prefab, spawn) as GameObject;
        if (inst == null)
            return null;
        Undo.RegisterCreatedObjectUndo(inst, "Spawn " + prefab.name);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        if (placeOnGround)
            PlaceFeetOnGround(inst.transform);
        return inst;
    }

    /// <summary>
    /// 将机甲 Mesh 根物体的世界坐标对齐到玩家出生点，并在水平面上朝向敌人出生点（Unity 前向为 +Z）。
    /// </summary>
    static void AlignPlayerMechBodyToSpawnFacingEnemy(GameObject playerRootInstance, Transform playerSpawn,
        Transform enemySpawn)
    {
        if (playerRootInstance == null || playerSpawn == null || enemySpawn == null)
            return;

        var meshBody = FindNamedTransformDepthFirst(playerRootInstance.transform, "Mesh");
        var alignTarget = meshBody != null ? meshBody : playerRootInstance.transform;

        Vector3 spawnPos = playerSpawn.position;
        Vector3 delta = spawnPos - alignTarget.position;
        playerRootInstance.transform.position += delta;

        Vector3 toEnemy = enemySpawn.position - alignTarget.position;
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude < 0.0001f)
            return;

        alignTarget.rotation = Quaternion.LookRotation(toEnemy.normalized, Vector3.up);
    }

    static Transform FindNamedTransformDepthFirst(Transform t, string targetName)
    {
        if (t.name == targetName)
            return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var hit = FindNamedTransformDepthFirst(t.GetChild(i), targetName);
            if (hit != null)
                return hit;
        }
        return null;
    }

    /// <summary>
    /// 优先用非 Trigger 的 Collider 包围盒对齐地面（与物理一致）；否则退回 Renderer。
    /// 略微抬高一点，减少与薄地面盒体的初始穿透被推飞。
    /// </summary>
    static void PlaceFeetOnGround(Transform t)
    {
        if (t == null)
            return;
        bool init = false;
        Bounds b = default;
        foreach (var c in t.GetComponentsInChildren<Collider>(true))
        {
            if (!c.enabled || c.isTrigger)
                continue;
            if (c.gameObject.GetComponentInParent<Canvas>(true) != null)
                continue;
            if (!init)
            {
                b = c.bounds;
                init = true;
            }
            else
                b.Encapsulate(c.bounds);
        }

        if (!init)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.GetComponentInParent<Canvas>(true) != null)
                    continue;
                if (!init)
                {
                    b = r.bounds;
                    init = true;
                }
                else
                    b.Encapsulate(r.bounds);
            }
        }

        if (!init)
            return;
        float skinLift = Mathf.Max(Physics.defaultContactOffset * 3f, 0.025f);
        float delta = -b.min.y + skinLift;
        if (Mathf.Abs(delta) > 0.0001f)
            t.position += new Vector3(0f, delta, 0f);
    }
}
