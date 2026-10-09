using UnityEngine;

/// <summary>Player movement and facing. Reads keyboard on desktop and a virtual stick on touch devices.</summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class BasketballPlayerController : MonoBehaviour
{
    [SerializeField, Min(.1f)] float moveSpeed = 3.2f;
    [SerializeField, Min(.1f)] float horizontalRange = 3f;
    [SerializeField, Min(.1f)] float forwardRange = 1f;
    [SerializeField, Min(.1f)] float backwardRange = 3.2f;

    Rigidbody body;
    Vector3 startPosition;
    Vector2 virtualMove;
    float aimYaw;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        startPosition = transform.position;
        aimYaw = transform.eulerAngles.y;
        body.isKinematic = false;
        body.useGravity = true;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public void SetVirtualMove(Vector2 move)
    {
        virtualMove = Vector2.ClampMagnitude(move, 1f);
    }

    public void SetAimYaw(float yaw)
    {
        aimYaw = yaw;
    }

    void FixedUpdate()
    {
        Vector2 move = virtualMove;

#if !(UNITY_ANDROID || UNITY_IOS)
        // Keyboard input only exists in non-mobile builds
        float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        if (Mathf.Abs(x) > 0f || Mathf.Abs(y) > 0f)
            move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#endif

        Quaternion facing = Quaternion.Euler(0f, aimYaw, 0f);
        Vector3 planar = facing * new Vector3(move.x, 0f, move.y) * moveSpeed;
        Vector3 velocity = body.linearVelocity;
        body.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
        if (move.sqrMagnitude > .01f && virtualMove.sqrMagnitude > .01f)
            body.MoveRotation(Quaternion.LookRotation(planar.normalized, Vector3.up));
        else
            body.MoveRotation(facing);

        Vector3 position = body.position;
        Vector3 delta = position - startPosition;
        delta.x = Mathf.Clamp(delta.x, -horizontalRange, horizontalRange);
        delta.z = Mathf.Clamp(delta.z, -backwardRange, forwardRange);
        body.MovePosition(startPosition + delta);
    }
}
