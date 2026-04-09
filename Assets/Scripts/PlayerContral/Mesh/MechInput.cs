using UnityEngine;

public class MechInput : MonoBehaviour//???????
{
    public Vector2 MoveAxis { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool BoostPressed { get; private set; }
    public bool DodgePressed { get; private set; }
    /// <summary>Tab ???????????????????????</summary>
    public bool OverBoostTogglePressed { get; private set; }
    /// <summary>????? ???? / ???????? C?</summary>
    public bool TurnModeTogglePressed { get; private set; }

    void Update()
    {
        MoveAxis = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        JumpPressed = Input.GetKeyDown(KeyCode.Space);
        JumpHeld = Input.GetKey(KeyCode.Space);
        BoostPressed = Input.GetKeyDown(KeyCode.LeftControl);
        DodgePressed = Input.GetKeyDown(KeyCode.LeftShift);
        OverBoostTogglePressed = Input.GetKeyDown(KeyCode.Tab);
        TurnModeTogglePressed = Input.GetKeyDown(KeyCode.C);
    }
}
