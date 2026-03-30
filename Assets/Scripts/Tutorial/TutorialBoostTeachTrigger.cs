using UnityEngine;

/// <summary>
/// 挂在带 Collider（Is Trigger）的 BoostTeach 物体上：玩家机甲进入后切换提示为加速推进说明。
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialBoostTeachTrigger : MonoBehaviour
{
    public LevelTutorialStep1 tutorial;

    void Awake()
    {
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

        tutorial.NotifyBoostTeachEntered();
    }
}
