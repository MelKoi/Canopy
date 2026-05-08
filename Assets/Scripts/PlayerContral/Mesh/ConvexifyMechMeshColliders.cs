using UnityEngine;

/// <summary>
/// 动态 Rigidbody 下若存在凹 MeshCollider，PhysX 会报错。
/// 在 Awake（早于默认脚本）时将扫描范围内所有 <see cref="MeshCollider"/> 设为 convex。
/// 武器预制体上 ProBuilder 等生成的凹网格碰撞体需如此处理，或改为 Primitive 碰撞体。
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class ConvexifyMechMeshColliders : MonoBehaviour
{
    [Tooltip("只处理该 Transform 及以下；不填则自动查找名为 Mesh 的子物体或含 LeftArm 的机甲子层级")]
    [SerializeField] Transform scanRoot;

    [SerializeField] bool includeInactive = true;

    void Awake()
    {
        Transform root = scanRoot != null ? scanRoot : DiscoverMechSubtree();
        if (root == null)
            root = transform;

        foreach (var mc in root.GetComponentsInChildren<MeshCollider>(includeInactive))
        {
            if (mc == null)
                continue;
            if (!mc.convex)
                mc.convex = true;
        }
    }

    Transform DiscoverMechSubtree()
    {
        var byName = transform.Find("Mesh");
        if (byName != null && MechWeaponMuzzleResolver.MeshRootHasLeftArm(byName))
            return byName;
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (MechWeaponMuzzleResolver.MeshRootHasLeftArm(c))
                return c;
        }

        return null;
    }
}
