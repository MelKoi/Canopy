using UnityEngine;

/// <summary>
/// 通用巡逻执行：沿 <see cref="EnemyPatrolPath"/> 给出的折线移动。
/// 正面可对齐子物体「enemyfront / Enemyfront」（相对根的水平方向），否则用 transform.forward。
/// 若同物体存在实现 <see cref="IEnemyPatrolSuspendCondition"/> 的组件（如战斗），满足条件时暂停巡逻。
/// </summary>
public class EnemyPatrolAgent : MonoBehaviour
{
    [Tooltip("未配置时自动查找子物体 enemyfront / Enemyfront")]
    public Transform enemyFront;

    Vector3[] _worldWaypoints;
    float[] _waitSeconds;
    float _moveSpeed = 2.2f;
    float _turnSpeedDeg = 360f;
    float _arriveDist = 0.45f;
    bool _loop = true;
    bool _hasPath;

    int _index;
    float _waitUntil;

    IEnemyPatrolSuspendCondition _suspendCondition;

    void Awake()
    {
        CacheSuspendCondition();
        if (enemyFront == null)
        {
            var t = transform.Find("enemyfront");
            if (t == null)
                t = transform.Find("Enemyfront");
            enemyFront = t;
        }
    }

    void CacheSuspendCondition()
    {
        foreach (var mb in GetComponents<MonoBehaviour>())
        {
            if (mb != null && mb is IEnemyPatrolSuspendCondition s)
            {
                _suspendCondition = s;
                return;
            }
        }
    }

    /// <summary>从场景中的路径配置拷贝世界空间路点（通常由敌人生成器在实例化后调用）。</summary>
    public void ApplyFromPath(EnemyPatrolPath path)
    {
        if (path == null || path.waypoints == null || path.waypoints.Count == 0)
        {
            _hasPath = false;
            return;
        }

        Transform anchor = path.transform;
        int n = path.waypoints.Count;
        _worldWaypoints = new Vector3[n];
        _waitSeconds = new float[n];
        for (int i = 0; i < n; i++)
        {
            _worldWaypoints[i] = anchor.TransformPoint(path.waypoints[i].localPosition);
            _waitSeconds[i] = Mathf.Max(0f, path.waypoints[i].waitSeconds);
        }

        _moveSpeed = path.moveSpeed;
        _turnSpeedDeg = path.turnSpeedDegrees;
        _arriveDist = path.arriveDistance;
        _loop = path.loop;
        _hasPath = true;
        _index = 0;
        _waitUntil = 0f;
    }

    void Update()
    {
        if (!_hasPath)
            return;

        if (_suspendCondition != null && _suspendCondition.ShouldSuspendPatrol())
            return;

        if (Time.time < _waitUntil)
            return;

        Vector3 target = _worldWaypoints[_index];
        Vector3 self = transform.position;
        Vector3 delta = target - self;
        float dist = delta.magnitude;

        Vector3 flatMove = delta;
        flatMove.y = 0f;
        FaceTowardsDirection(flatMove, Time.deltaTime);

        if (dist <= _arriveDist)
        {
            _waitUntil = Time.time + _waitSeconds[_index];
            if (_index + 1 >= _worldWaypoints.Length)
            {
                if (_loop)
                    _index = 0;
                else
                    _hasPath = false;
            }
            else
                _index++;
            return;
        }

        transform.position = Vector3.MoveTowards(self, target, _moveSpeed * Time.deltaTime);
    }

    void LateUpdate()
    {
        if (enemyFront == null)
            return;
        if (enemyFront.parent != transform)
            return;
        enemyFront.localRotation = Quaternion.identity;
    }

    void FaceTowardsDirection(Vector3 horizontalDir, float dt)
    {
        if (horizontalDir.sqrMagnitude < 0.0001f)
            return;

        Vector3 want = horizontalDir.normalized;
        Vector3 face;
        if (enemyFront != null)
        {
            face = enemyFront.position - transform.position;
            face.y = 0f;
        }
        else
            face = transform.forward;

        if (face.sqrMagnitude < 0.0001f)
            return;

        float signed = Vector3.SignedAngle(face.normalized, want, Vector3.up);
        float maxStep = _turnSpeedDeg * dt;
        transform.Rotate(0f, Mathf.Clamp(signed, -maxStep, maxStep), 0f, Space.World);
    }
}
