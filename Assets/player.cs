using UnityEngine;
using UnityEngine.InputSystem;
public class Player : MonoBehaviour
{
    PlayerInputActions input;
    Vector2 moveInput;
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float jumpHeight = 8f;
    [SerializeField] LayerMask groundLayer;

    [Header("Wheels")]
    [SerializeField] Transform frontWheel;
    [SerializeField] Transform rearWheel;
    [SerializeField] float suspensionRestLength = 0.4f;
    [SerializeField] float suspensionStiffness = 5f;
    [SerializeField] float suspensionDamping = 1f;
    [SerializeField] float jumpWheelSquash = 0.08f;
    [SerializeField] float landingWheelSquash = 0.12f;
    [SerializeField] float wheelSquashDuration = 0.18f;

    SpriteRenderer spriteRenderer;
    bool isGrounded;
    float verticalVelocity;

    // Squash animation — driven by a sine arc over squashTimer
    float squashTimer;
    float squashMagnitude;
    // Visual-only offsets kept separately so suspension spring operates on real positions
    float frontSquashOffset;
    float rearSquashOffset;

    float frontWheelVel;
    float rearWheelVel;
    float frontWheelAngle;
    float rearWheelAngle;
    CircleCollider2D frontWheelCollider;
    CircleCollider2D rearWheelCollider;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        if (frontWheel != null) frontWheelCollider = frontWheel.GetComponent<CircleCollider2D>();
        if (rearWheel != null) rearWheelCollider = rearWheel.GetComponent<CircleCollider2D>();

        input = new PlayerInputActions();
        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        input.Player.Jump.performed += ctx => Jump();
    }

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();

    void Update()
    {
        verticalVelocity += Physics2D.gravity.y * Time.deltaTime;

        float dx = moveInput.x * moveSpeed * Time.deltaTime;
        transform.Translate(Vector2.right * dx);

        if (moveInput.x != 0f && spriteRenderer != null)
            spriteRenderer.flipX = moveInput.x < 0f;

        // Remove last frame's visual squash so the suspension spring operates on real positions.
        // This prevents the spring from drifting due to the squash offset in its currentY read.
        if (frontWheel != null) frontWheel.localPosition -= new Vector3(0f, frontSquashOffset, 0f);
        if (rearWheel != null) rearWheel.localPosition -= new Vector3(0f, rearSquashOffset, 0f);

        // Wheels are the sole ground detectors
        bool frontGrounded = UpdateWheelSuspension(frontWheel, frontWheelCollider, ref frontWheelVel, out float frontGroundY);
        bool rearGrounded = UpdateWheelSuspension(rearWheel, rearWheelCollider, ref rearWheelVel, out float rearGroundY);
        isGrounded = frontGrounded || rearGrounded;

        if (isGrounded && verticalVelocity < 0f)
        {
            float groundY = frontGrounded ? frontGroundY : rearGroundY;
            float radius = frontGrounded && frontWheelCollider != null ? frontWheelCollider.radius
                           : rearWheelCollider != null ? rearWheelCollider.radius : 0.1f;
            float landingY = groundY + radius + suspensionRestLength;

            // Stop if this frame's fall would reach or pass the landing point,
            // preventing the one-frame overshoot that causes the upward snap.
            float remainingFall = transform.position.y - landingY;
            if (remainingFall <= -verticalVelocity * Time.deltaTime)
            {
                // Trigger landing squash proportional to impact speed
                squashMagnitude = Mathf.Min(landingWheelSquash, landingWheelSquash * (-verticalVelocity / jumpHeight));
                squashTimer = wheelSquashDuration;

                verticalVelocity = 0f;
                Vector3 pos = transform.position;
                pos.y = landingY;
                transform.position = pos;
            }
        }

        transform.Translate(Vector2.up * verticalVelocity * Time.deltaTime);

        // Advance squash animation and compute new offsets
        if (squashTimer > 0f)
        {
            squashTimer = Mathf.Max(0f, squashTimer - Time.deltaTime);
            float t = 1f - squashTimer / wheelSquashDuration; // 0→1 over duration
            float offset = squashMagnitude * Mathf.Sin(t * Mathf.PI); // rises then falls
            frontSquashOffset = rearSquashOffset = offset;
        }
        else
        {
            frontSquashOffset = rearSquashOffset = 0f;
        }

        // Apply squash on top of suspension result
        ApplyWheelSquash(frontWheel, frontWheelCollider, frontSquashOffset);
        ApplyWheelSquash(rearWheel, rearWheelCollider, rearSquashOffset);

        // Spin wheels proportional to horizontal travel
        SpinWheel(frontWheel, frontWheelCollider, dx, ref frontWheelAngle);
        SpinWheel(rearWheel, rearWheelCollider, dx, ref rearWheelAngle);
    }

    // Returns whether the wheel is touching ground; outputs the world Y of the ground surface.
    bool UpdateWheelSuspension(Transform wheel, CircleCollider2D col, ref float velocity, out float groundWorldY)
    {
        groundWorldY = 0f;
        if (wheel == null) return false;

        float radius = col != null ? col.radius : 0.1f;

        Vector2 attachWorld = transform.TransformPoint(new Vector3(wheel.localPosition.x, 0f, 0f));
        float castDist = suspensionRestLength + radius + 0.5f;
        RaycastHit2D hit = Physics2D.Raycast(attachWorld, Vector2.down, castDist, groundLayer);

        float currentY = wheel.localPosition.y;
        bool grounded = false;
        float targetY = -suspensionRestLength;

        if (hit.collider != null)
        {
            groundWorldY = hit.point.y;
            float wheelWorldY = groundWorldY + radius;
            float rawTargetY = transform.InverseTransformPoint(new Vector3(0f, wheelWorldY, 0f)).y;

            grounded = rawTargetY >= (-suspensionRestLength - radius);
            if (grounded)
                targetY = Mathf.Clamp(rawTargetY, -suspensionRestLength - radius, -radius);
        }

        float newY;
        if (grounded && Mathf.Abs(verticalVelocity) < 1f)
        {
            // Chassis at rest — hard-snap wheel to track the surface precisely.
            newY = targetY;
            velocity = 0f;
        }
        else
        {
            // Falling or airborne: spring toward target so the wheel doesn't visually
            // pop downward the moment the raycast first detects ground below.
            float springForce = (targetY - currentY) * suspensionStiffness;
            velocity += springForce * Time.deltaTime;
            velocity *= Mathf.Clamp01(1f - suspensionDamping * Time.deltaTime);
            newY = currentY + velocity * Time.deltaTime;
        }

        wheel.localPosition = new Vector3(wheel.localPosition.x, newY, wheel.localPosition.z);
        return grounded;
    }

    void SpinWheel(Transform wheel, CircleCollider2D col, float dx, ref float angle)
    {
        if (wheel == null) return;
        float radius = col != null ? col.radius : 0.1f;
        angle -= (dx / (2f * Mathf.PI * radius)) * 360f;
        // Set world-space rotation to avoid distortion from non-uniform parent scale
        wheel.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    static void ApplyWheelSquash(Transform wheel, CircleCollider2D col, float offset)
    {
        if (wheel == null) return;
        float radius = col != null ? col.radius : 0.1f;
        var p = wheel.localPosition;
        p.y = Mathf.Min(p.y + offset, -radius);
        wheel.localPosition = p;
    }

    void Jump()
    {
        if (isGrounded)
        {
            squashMagnitude = jumpWheelSquash;
            squashTimer = wheelSquashDuration;
            verticalVelocity = jumpHeight;
        }
    }
}
