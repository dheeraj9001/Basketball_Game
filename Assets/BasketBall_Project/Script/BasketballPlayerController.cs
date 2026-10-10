using UnityEngine;

/// <summary>Player movement and facing. Reads keyboard on desktop and a virtual stick on touch devices.</summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class BasketballPlayerController : MonoBehaviour
{
    const float GroundProbeStart = .2f;
    const float GroundProbeDistance = .45f;
    const float GroundSnapTolerance = .5f;

    [SerializeField, Min(.1f)] float moveSpeed = 3.2f;
    [SerializeField, Min(.1f)] float horizontalRange = 3f;
    [SerializeField, Min(.1f)] float forwardRange = 5f;
    [SerializeField, Min(.1f)] float backwardRange = 5f;

    Rigidbody body;
    CapsuleCollider capsule;
    readonly RaycastHit[] groundHits = new RaycastHit[32];
    Vector3 startPosition;
    Vector2 virtualMove;
    float aimYaw;
    bool visualGroundAligned;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = .25f;
        capsule.height = 1.75f;
        capsule.center = new Vector3(0f, 1.68f, 0f);
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
        Quaternion movementFrame = Quaternion.Euler(0f, aimYaw, 0f);
        Vector3 localDelta = Quaternion.Inverse(movementFrame) * delta;
        localDelta.x = Mathf.Clamp(localDelta.x, -horizontalRange, horizontalRange);
        localDelta.z = Mathf.Clamp(localDelta.z, -backwardRange, forwardRange);
        Vector3 targetPosition = startPosition + movementFrame * localDelta;
        Vector3 probeOrigin = body.position + Vector3.up * GroundProbeStart;
        int hitCount = Physics.RaycastNonAlloc(probeOrigin, Vector3.down, groundHits,
            GroundProbeStart + GroundProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool foundGround = false;
        RaycastHit groundHit = default;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = groundHits[i].collider;
            if (hitCollider == null || hitCollider == capsule || hitCollider.transform.IsChildOf(transform)) continue;
            if (groundHits[i].normal.y < .5f || groundHits[i].distance >= closestDistance) continue;
            closestDistance = groundHits[i].distance;
            groundHit = groundHits[i];
            foundGround = true;
        }

        if (foundGround)
        {
            float feetOffset = capsule.bounds.min.y - body.position.y;
            float groundedY = groundHit.point.y - feetOffset;
            if (targetPosition.y <= groundedY + GroundSnapTolerance && body.linearVelocity.y <= 0f)
            {
                targetPosition.y = groundedY;
                body.linearVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
            }
        }

        body.MovePosition(targetPosition);
    }

    void LateUpdate()
    {
        if (visualGroundAligned) return;
        visualGroundAligned = true;

        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        Renderer[] renderers = animator.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0 || !TryFindGround(out RaycastHit groundHit)) return;

        Bounds visualBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) visualBounds.Encapsulate(renderers[i].bounds);

        float offset = groundHit.point.y - visualBounds.min.y;
        if (Mathf.Abs(offset) < .03f || Mathf.Abs(offset) > 1f) return;

        animator.transform.position += Vector3.up * offset;
        Transform handAnchor = transform.Find("Right Hand Ball Anchor");
        if (handAnchor != null) handAnchor.localPosition += Vector3.up * offset;
    }

    bool TryFindGround(out RaycastHit groundHit)
    {
        Vector3 origin = body.position + Vector3.up * 2f;
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 4f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool foundGround = false;
        float closestDistance = float.PositiveInfinity;
        groundHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = groundHits[i].collider;
            if (hitCollider == null || hitCollider == capsule || hitCollider.transform.IsChildOf(transform)) continue;
            if (groundHits[i].normal.y < .5f || groundHits[i].distance >= closestDistance) continue;
            closestDistance = groundHits[i].distance;
            groundHit = groundHits[i];
            foundGround = true;
        }

        return foundGround;
    }
}
