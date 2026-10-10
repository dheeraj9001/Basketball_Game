using System.Collections.Generic;
using UnityEngine;
using PinePie.SimpleJoystick;

/// <summary>Controls the player, basketball, shot preview, and existing Canvas UI.</summary>
public sealed class BasketballGameController : MonoBehaviour
{
    [Header("Scene references")]
    public Transform playerRoot;
    public Transform handAnchor;
    public Transform basketball;
    public Transform hoopTarget;
    public LineRenderer arcPreview;
    public Camera gameCamera;
    public BasketballPlayerController playerController;
    public JoystickController movementJoystick;
    public BasketballCanvasUI canvasUI;
    public HoopScoreTrigger scoreTrigger;
    public Animator playerAnimator;

    [Header("Shot setup")]
    [Min(0.1f)] public float rimHeight = 3.05f;
    [Min(0.1f)] public float ballDiameter = 0.24f;
    [Range(25f, 75f)] public float launchAngle = 52f;
    [Min(0.1f)] public float chargeSeconds = 1.4f;

    const float Gravity = 9.81f;
    const float MinSpeed = 4f;
    const float MaxSpeed = 14f;

    readonly List<Vector3> arcPoints = new List<Vector3>(180);
    MaterialPropertyBlock arcProperties;
    Rigidbody ballBody;
    Collider ballCollider;
    Vector3 hoopCenter;
    Vector3 shotDirection = Vector3.forward;
    Vector3 ballSpawnPosition;
    float yaw;
    float charge;
    int aimPointerId = -1;
    Vector2 lastAimPointer;
    Vector2 joystickValue;
    int score;
    int attempts;
    bool held;
    bool charging;
    bool shotInFlight;
    string resultText = "Move to the ball and press Grab/Aim";

    void Awake()
    {

        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
        }

        arcProperties = new MaterialPropertyBlock();
        Physics.gravity = new Vector3(0f, -Gravity, 0f);
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.LandscapeLeft;

        yaw = playerRoot.eulerAngles.y;
        ballBody = basketball.GetComponent<Rigidbody>();
        ballCollider = basketball.GetComponent<Collider>();
        hoopCenter = hoopTarget.position;
        ballSpawnPosition = basketball.position;
        arcPreview.enabled = true;
        arcPreview.useWorldSpace = true;
        arcPreview.startWidth = .055f;
        arcPreview.endWidth = .03f;
        arcPreview.numCornerVertices = 4;
        arcPreview.numCapVertices = 4;
        arcPreview.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arcPreview.receiveShadows = false;
        arcPreview.positionCount = 0;

        ballBody.linearVelocity = Vector3.zero;
        ballBody.angularVelocity = Vector3.zero;
        ballBody.isKinematic = true;
        scoreTrigger.Initialize(this);
        canvasUI.Initialize(this);
    }

    void Update()
    {
        ReadAimInput();
        if (!Application.isMobilePlatform)
        {
            if (Input.GetKeyDown(KeyCode.E)) GrabOrAim();
            if (held && Input.GetKeyDown(KeyCode.Space)) BeginCharge();
            if (held && Input.GetKeyUp(KeyCode.Space)) ReleaseShot();
        }

        joystickValue = movementJoystick.InputDirection;
        playerController.SetAimYaw(yaw);
        playerController.SetVirtualMove(joystickValue);
        canvasUI.Refresh(score, attempts, resultText, charging, charge);

        if (playerAnimator != null)
        {
            bool moving = joystickValue.sqrMagnitude > 0.01f;
#if !(UNITY_ANDROID || UNITY_IOS)
            moving |= Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A)
                   || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D)
                   || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow)
                   || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);
#endif
            playerAnimator.SetBool("IsWalking", moving && !charging);
            playerAnimator.SetBool("IsAiming", charging);
        }

        if (charging) charge = Mathf.Clamp01(charge + Time.deltaTime / chargeSeconds);
        if (held) UpdateArc(); else arcPreview.positionCount = 0;
        UpdateCamera();
        if (shotInFlight && basketball.position.y < -2f) FinishShot(false);
    }

    void ReadAimInput()
    {
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                Vector2 position = touch.position;
                if (touch.phase == TouchPhase.Began && position.x > Screen.width * .43f && position.y > Screen.height * .25f && !shotInFlight)
                {
                    aimPointerId = touch.fingerId;
                    lastAimPointer = position;
                }
                if (touch.fingerId == aimPointerId)
                {
                    if (touch.phase == TouchPhase.Moved) ApplyAimDelta(position);
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) aimPointerId = -1;
                }
            }
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 position = Input.mousePosition;
            if (position.x > Screen.width * .43f && position.y > Screen.height * .25f)
            {
                aimPointerId = 9999;
                lastAimPointer = position;
            }
        }
        if (aimPointerId == 9999 && Input.GetMouseButton(0)) ApplyAimDelta(Input.mousePosition);
        if (Input.GetMouseButtonUp(0) && aimPointerId == 9999) aimPointerId = -1;
    }

    void ApplyAimDelta(Vector2 position)
    {
        Vector2 delta = position - lastAimPointer;
        yaw += delta.x * .18f;
        launchAngle = Mathf.Clamp(launchAngle + delta.y * .08f, 25f, 75f);
        lastAimPointer = position;
    }

    void UpdateCamera()
    {
        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        Vector3 target = playerRoot.position + heading * new Vector3(0f, 3f, -6.2f);
        float follow = 1f - Mathf.Exp(-5f * Time.deltaTime);
        gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, target, follow);
        Vector3 look = Vector3.Lerp(playerRoot.position + Vector3.up * 1.25f, hoopCenter + Vector3.up * .1f, .32f);
        gameCamera.transform.rotation = Quaternion.Slerp(gameCamera.transform.rotation, Quaternion.LookRotation(look - gameCamera.transform.position, Vector3.up), follow);
    }

    bool CanGrab() => !held && !shotInFlight && Vector3.Distance(playerRoot.position, basketball.position) <= 3f;

    public void GrabOrAim()
    {
        if (held)
        {
            resultText = "Drag to aim, hold SHOOT to charge";
            return;
        }
        if (shotInFlight) return;
        if (!CanGrab())
        {
            resultText = "Move closer to the ball";
            return;
        }

        held = true;
        ballBody.linearVelocity = Vector3.zero;
        ballBody.angularVelocity = Vector3.zero;
        ballBody.isKinematic = true;
        ballCollider.enabled = false;
        basketball.SetParent(handAnchor, false);
        basketball.localPosition = Vector3.zero;
        resultText = "Drag to aim, hold SHOOT to charge";
    }

    public void BeginCharge()
    {
        if (!held || shotInFlight) return;
        charging = true;
        charge = 0f;
    }

    public void ReleaseShot()
    {
        if (!charging || !held) { charging = false; return; }
        if (playerAnimator != null)
            playerAnimator.Play("Base Layer.Aim", 0, 1f);
        charging = false;
        float speed = Mathf.Lerp(MinSpeed, MaxSpeed, charge);
        shotDirection = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        float angle = launchAngle * Mathf.Deg2Rad;
        Vector3 velocity = shotDirection * (speed * Mathf.Cos(angle)) + Vector3.up * (speed * Mathf.Sin(angle));
        basketball.SetParent(null, true);
        ballBody.isKinematic = false;
        ballCollider.enabled = true;
        ballBody.linearVelocity = velocity;
        ballBody.angularVelocity = Vector3.Cross(Vector3.up, shotDirection) * 18f;
        held = false;
        shotInFlight = true;
        attempts++;
        resultText = "Shot in flight";
    }

    void UpdateArc()
    {
        shotDirection = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        float speed = charging ? Mathf.Lerp(MinSpeed, MaxSpeed, charge) : Mathf.Lerp(MinSpeed, MaxSpeed, .25f);
        float angle = launchAngle * Mathf.Deg2Rad;
        Vector3 velocity = shotDirection * (speed * Mathf.Cos(angle)) + Vector3.up * (speed * Mathf.Sin(angle));
        Vector3 point = handAnchor.position;
        Vector3 stepVelocity = velocity;
        float stepTime = Time.fixedDeltaTime;
        arcPoints.Clear();
        bool possibleGoal = false;

        for (int i = 0; i < 300; i++)
        {
            arcPoints.Add(point);
            stepVelocity += Physics.gravity * stepTime;
            Vector3 next = point + stepVelocity * stepTime;
            if (CrossesHoop(point, next, stepVelocity.y)) possibleGoal = true;
            point = next;
            if (point.y < 0f) { arcPoints.Add(point); break; }
        }

        arcPreview.positionCount = arcPoints.Count;
        arcPreview.SetPositions(arcPoints.ToArray());
        Color color = possibleGoal ? new Color(.08f, 1f, .18f, 1f) : new Color(1f, .28f, .12f, 1f);
        arcPreview.startColor = color;
        arcPreview.endColor = color;
        arcProperties.SetColor("_BaseColor", color);
        arcProperties.SetColor("_Color", color);
        arcPreview.SetPropertyBlock(arcProperties);
    }

    bool CrossesHoop(Vector3 start, Vector3 end, float verticalSpeed)
    {
        if (verticalSpeed >= 0f || start.y < hoopCenter.y || end.y > hoopCenter.y || Mathf.Abs(end.y - start.y) < .00001f) return false;
        float t = (hoopCenter.y - start.y) / (end.y - start.y);
        Vector3 crossing = Vector3.Lerp(start, end, t);
        Vector2 offset = new Vector2(crossing.x - hoopCenter.x, crossing.z - hoopCenter.z);
        return offset.magnitude <= .12f;
    }

    public void ScoreBall()
    {
        if (!shotInFlight || ballBody.linearVelocity.y >= 0f) return;
        score++;
        FinishShot(true);
    }

    void FinishShot(bool made)
    {
        if (!shotInFlight) return;
        shotInFlight = false;
        resultText = made ? "BUCKET!" : "Miss - reset for another shot";
        held = false;
        Invoke(nameof(RespawnBall), made ? 1.1f : .7f);
    }

    void RespawnBall()
    {
        ballBody.linearVelocity = Vector3.zero;
        ballBody.angularVelocity = Vector3.zero;
        ballBody.isKinematic = true;
        basketball.position = ballSpawnPosition;
        basketball.rotation = Quaternion.identity;
        ballCollider.enabled = true;
        resultText = "Move to the ball and press Grab/Aim";
    }
}
