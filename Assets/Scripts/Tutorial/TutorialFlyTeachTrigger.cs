using UnityEngine;

/// <summary>
/// 挂在带 Collider（Is Trigger）的 FlyTeach 物体上：玩家机甲进入后播放浮空教学叙事与 Teach 提示。
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialFlyTeachTrigger : MonoBehaviour
{
    public LevelTutorialStep1 tutorial;

    void Awake()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
        if (tutorial == null)
            tutorial = FindFirstObjectByType<LevelTutorialStep1>(FindObjectsInactive.Include);
    }

    void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (tutorial == null)
            return;

        if (other.GetComponentInParent<MechController>() == null)
            return;

        tutorial.NotifyFlyTeachEntered();
    }
}
