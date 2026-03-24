/// <summary>
/// 可选能力：挂在与 <see cref="EnemyPatrolAgent"/> 同一物体上的战斗/警戒脚本可实现此接口，
/// 在需要追击或交火时返回 true 以暂停巡逻。
/// </summary>
public interface IEnemyPatrolSuspendCondition
{
    bool ShouldSuspendPatrol();
}
