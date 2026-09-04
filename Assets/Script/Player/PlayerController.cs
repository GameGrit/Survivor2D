using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Tooltip("自由方向摇杆（替换旧的8方向SimpleJoystick）")]
    public FreeJoystickController joystickController;


    private Rigidbody2D rb;
    [HideInInspector] public float moveSpeed; // 运行时从PlayerExp读，不在Inspector填
    public Vector2 dir;
    void Start()
    {
        moveSpeed = PlayerExp.Instance.moveSpeed;
        rb = GetComponent<Rigidbody2D>();
    }


    void Update()
    {
        dir = joystickController.InputDirection;
        rb.velocity = dir * moveSpeed;
        if (dir.magnitude < 0.01f)
            return;

        //Vector3 moveDelta = new Vector3(dir.x, dir.y, 0) * moveSpeed * Time.deltaTime;
        //rb.MovePosition(transform.position + moveDelta);

    }
}
