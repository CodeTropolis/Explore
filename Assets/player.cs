using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    PlayerInputActions input;
    Vector2 moveInput;

    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float deceleration = 20f;
    [SerializeField] float jumpHeight = 8f;
    [SerializeField] LayerMask groundLayer;

    [Header("Wheels")]
    [SerializeField] Transform frontWheel;
    [SerializeField] Transform rearWheel;

    [Header("Chassis 'Bob'")]
    [SerializeField] float bobAmount = 0.04f;
    [SerializeField] float bobFrequency = 12.0f;

    [Header("Rotation")]
    [SerializeField] float rotationSpeed = 10f;

    SpriteRenderer spriteRenderer;
    float verticalVelocity;
    float horizontalVelocity;
    bool isGrounded;

    float chassisBobPhase;
    float chassisBobOffset;

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
        // Remove last frame bob before calculations
        transform.position -= new Vector3(0f, chassisBobOffset, 0f);

        verticalVelocity += Physics2D.gravity.y * Time.deltaTime;

        float targetSpeed = moveInput.x * moveSpeed;
        if (moveInput.x == 0f)
            horizontalVelocity = Mathf.MoveTowards(horizontalVelocity, 0f, deceleration * Time.deltaTime);
        else
            horizontalVelocity = targetSpeed;

        float dx = horizontalVelocity * Time.deltaTime;
        transform.Translate(Vector2.right * dx);

        if (horizontalVelocity != 0f && spriteRenderer != null)
            spriteRenderer.flipX = horizontalVelocity < 0f;

        // Ground + slope handling
        CheckGroundAndRotate();

        if (isGrounded)
            verticalVelocity = 0f;
        else
            transform.Translate(Vector2.up * verticalVelocity * Time.deltaTime);

        SpinWheel(frontWheel, frontWheelCollider, dx, ref frontWheelAngle);
        SpinWheel(rearWheel, rearWheelCollider, dx, ref rearWheelAngle);

        // Chassis bob
        if (isGrounded && horizontalVelocity != 0f)
        {
            chassisBobPhase += Mathf.Abs(dx) * bobFrequency;
            chassisBobOffset = Mathf.Sin(chassisBobPhase) * bobAmount;
        }
        else
        {
            chassisBobOffset = 0f;
        }

        transform.position += new Vector3(0f, chassisBobOffset, 0f);
    }

    void CheckGroundAndRotate()
    {
        bool frontGrounded = WheelGroundCheck(frontWheel, frontWheelCollider, out float frontTargetY, out Vector2 frontNormal);
        bool rearGrounded = WheelGroundCheck(rearWheel, rearWheelCollider, out float rearTargetY, out Vector2 rearNormal);

        if ((frontGrounded || rearGrounded) && verticalVelocity <= 0f)
        {
            isGrounded = true;

            float targetY = frontGrounded && rearGrounded
                ? Mathf.Max(frontTargetY, rearTargetY)
                : frontGrounded ? frontTargetY : rearTargetY;

            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, targetY, 10f * Time.deltaTime);
            transform.position = pos;

            // When both wheels hit, their normals are averaged 
            // (handles bumpy transitions); when only one hits, 
            // that normal alone drives the rotation
            Vector2 normal = frontGrounded && rearGrounded
                ? ((frontNormal + rearNormal) * 0.5f).normalized
                : frontGrounded ? frontNormal : rearNormal;

            float targetAngle = Mathf.Atan2(-normal.x, normal.y) * Mathf.Rad2Deg;
            float smoothed = Mathf.LerpAngle(transform.eulerAngles.z, targetAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, smoothed);
        }
        else
        {
            isGrounded = false;
        }
    }

    bool WheelGroundCheck(Transform wheel, CircleCollider2D col, out float targetChassisY, out Vector2 normal)
    {
        targetChassisY = 0f;
        normal = Vector2.up;
        if (wheel == null) return false;

        float radius = col != null ? col.radius : 0.1f;
        Vector2 origin = wheel.position;
        float castDist = Mathf.Abs(wheel.localPosition.y) + radius + 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, castDist, groundLayer);

        if (hit.collider != null)
        {
            targetChassisY = hit.point.y + radius - wheel.localPosition.y;
            normal = hit.normal;
            return true;
        }
        return false;
    }

    void SpinWheel(Transform wheel, CircleCollider2D col, float dx, ref float angle)
    {
        if (wheel == null) return;

        float radius = col != null ? col.radius : 0.1f;
        angle -= dx / (2f * Mathf.PI * radius) * 360f;
        wheel.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void Jump()
    {
        if (isGrounded)
            verticalVelocity = jumpHeight;
    }
}