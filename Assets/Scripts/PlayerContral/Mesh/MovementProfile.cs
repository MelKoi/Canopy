using UnityEngine;

[CreateAssetMenu(menuName ="Mech/Movement Profile")]//移动状态的ScriptObject
public class MovementProfile : ScriptableObject
{
    public float maxSpeed = 10f;//普通移动的最大速度
    public float acceleration = 40f;//加速度
    public float deceleration = 30f;//减速度
    //以上两个应该是调整手感的数值，与基本的移动实现无关
    public float energyCostPerSecond = 0f;//每秒能量消耗
}
