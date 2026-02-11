using UnityEngine;

public class LockOnSystem : MonoBehaviour//在一定范围内锁定敌人（只锁定单个敌人）
{
    public Camera cam;
    public Rect lockRect = new Rect(0.4f, 0.4f, 0.2f, 0.2f); // 归一化屏幕空间
    public LayerMask enemyLayer;

    public Transform currentTarget;

    void Update()
    {
        if (currentTarget == null)
            DetectEnemy();
    }

    void LateUpdate()//敌人没有位移或者高速移出锁定范围时，保持追踪（硬锁定）
    {
        if (currentTarget == null) return;

        Vector3 viewportPos = cam.WorldToViewportPoint(currentTarget.position);

        if (!lockRect.Contains(new Vector2(viewportPos.x, viewportPos.y)))
        {
            currentTarget = null; // 丢失锁定
        }
    }


    void DetectEnemy()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, 100f, enemyLayer);//此API返回一个以玩家机体为圆心，参数2为半径，满足enemylayer条件的collider集合
        foreach (var e in enemies)//对于上述API获取到的集合中的每一项
        {
            Vector3 screenPos = cam.WorldToViewportPoint(e.transform.position);
            if (screenPos.z < 0) continue;

            if (lockRect.Contains(new Vector2(screenPos.x, screenPos.y)))//如果敌人在这个矩形范围内
            {
                currentTarget = e.transform;//锁定目标
                break;
            }
        }
    }
}

