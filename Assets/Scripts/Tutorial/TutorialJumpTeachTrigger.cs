using UnityEngine;

/// <summary>
/// 挂在带 BoxCollider（Is Trigger）的 JumpTeach 物体上：玩家机甲进入后通知教学流程。
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialJumpTeachTrigger : MonoBehaviour
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

        tutorial.NotifyJumpTeachEntered();
    }
}
