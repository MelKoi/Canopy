using UnityEngine;

public class MechInput : MonoBehaviour//ªÒ»° ‰»Î
{
    public Vector2 MoveAxis { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool BoostPressed { get; private set; }
    public bool DodgePressed { get; private set; }
    public bool OverBoostHeld { get; private set; }

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
        OverBoostHeld = Input.GetKey(KeyCode.Tab);
    }
}
