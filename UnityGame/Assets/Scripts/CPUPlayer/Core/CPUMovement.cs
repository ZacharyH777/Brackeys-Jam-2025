using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class CPUMovement : MonoBehaviour
{
    [Header("Movement Parameters")]
    [Tooltip("Maximum movement speed")]
    public float max_speed = 12f;
    [Tooltip("Acceleration toward target")]
    public float acceleration = 60f;
    [Tooltip("Deceleration when no target or reached target")]
    public float deceleration = 80f;
    [Tooltip("Distance threshold to consider target reached")]
    public float target_threshold = 0.2f;

    [Header("CPU Behavior")]
    [Tooltip("How smoothly CPU moves toward target (0-1)")]
    public float movement_smoothing = 0.8f;
    [Tooltip("Maximum distance CPU will move in one decision")]
    public float max_movement_distance = 5f;

    [Header("Targets")]
    [Tooltip("Optional PingPongMovement to sync with for animations")]
    public PingPongMovement ping_pong_target;

    private Rigidbody2D rigidbody2d;
    private Vector2 current_target_position;
    private Vector2 smoothed_target_position;
    private bool has_target;

    void Awake()
    {
        rigidbody2d = GetComponent<Rigidbody2D>();
        
        // Configure rigidbody similar to PlayerMovement
        rigidbody2d.gravityScale = 0f;
        rigidbody2d.interpolation = RigidbodyInterpolation2D.Interpolate;
        rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
#if UNITY_6000_0_OR_NEWER
        rigidbody2d.linearDamping = 0f;
        rigidbody2d.angularDamping = 0f;
#else
        rigidbody2d.drag = 0f;
        rigidbody2d.angularDrag = 0f;
#endif
        rigidbody2d.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Initialize target to current position
        current_target_position = transform.position;
        smoothed_target_position = current_target_position;
    }

    void FixedUpdate()
    {
        if (!has_target)
        {
            // No target, apply deceleration
            ApplyDeceleration();
            return;
        }

        // Smooth the target position to make movement less jittery
        smoothed_target_position = Vector2.Lerp(smoothed_target_position, current_target_position, movement_smoothing * Time.fixedDeltaTime * 5f);

        Vector2 current_position = transform.position;
        Vector2 direction_to_target = smoothed_target_position - current_position;
        float distance_to_target = direction_to_target.magnitude;

        // Check if we're close enough to target
        if (distance_to_target <= target_threshold)
        {
            // Close enough, apply deceleration
            ApplyDeceleration();
            return;
        }

        // Calculate desired velocity toward target
        Vector2 desired_direction = direction_to_target.normalized;
        Vector2 desired_velocity = desired_direction * max_speed;

        // Apply acceleration toward desired velocity
        ApplyAccelerationTowardVelocity(desired_velocity);

        // Cap maximum speed
        CapMaxSpeed();

        // Update ping pong target for animations if available
        UpdatePingPongTarget();
    }

    private void ApplyAccelerationTowardVelocity(Vector2 desired_velocity)
    {
        float dt = Time.fixedDeltaTime;
#if UNITY_6000_0_OR_NEWER
        Vector2 current_velocity = rigidbody2d.linearVelocity;
#else
        Vector2 current_velocity = rigidbody2d.velocity;
#endif

        Vector2 velocity_diff = desired_velocity - current_velocity;
        float max_velocity_change = acceleration * dt;
        
        if (velocity_diff.magnitude > max_velocity_change)
        {
            velocity_diff = velocity_diff.normalized * max_velocity_change;
        }

        rigidbody2d.AddForce(rigidbody2d.mass * velocity_diff, ForceMode2D.Impulse);
    }

    private void ApplyDeceleration()
    {
        float dt = Time.fixedDeltaTime;
#if UNITY_6000_0_OR_NEWER
        Vector2 current_velocity = rigidbody2d.linearVelocity;
#else
        Vector2 current_velocity = rigidbody2d.velocity;
#endif

        float current_speed = current_velocity.magnitude;
        if (current_speed > 0f)
        {
            float new_speed = Mathf.Max(0f, current_speed - deceleration * dt);
            Vector2 new_velocity = current_velocity.normalized * new_speed;
            
            Vector2 velocity_change = new_velocity - current_velocity;
            rigidbody2d.AddForce(rigidbody2d.mass * velocity_change, ForceMode2D.Impulse);
        }
    }

    private void CapMaxSpeed()
    {
#if UNITY_6000_0_OR_NEWER
        Vector2 velocity = rigidbody2d.linearVelocity;
#else
        Vector2 velocity = rigidbody2d.velocity;
#endif

        if (velocity.magnitude > max_speed)
        {
            velocity = velocity.normalized * max_speed;
#if UNITY_6000_0_OR_NEWER
            rigidbody2d.linearVelocity = velocity;
#else
            rigidbody2d.velocity = velocity;
#endif
        }
    }

    private void UpdatePingPongTarget()
    {
        if (ping_pong_target == null) return;

        // Create a synthetic input vector based on our movement direction
#if UNITY_6000_0_OR_NEWER
        Vector2 velocity = rigidbody2d.linearVelocity;
#else
        Vector2 velocity = rigidbody2d.velocity;
#endif

        // Normalize velocity to create input-like values
        Vector2 synthetic_input = Vector2.zero;
        if (velocity.magnitude > 0.1f) // Only if moving significantly
        {
            synthetic_input = velocity.normalized;
            // Scale down to reasonable input range
            synthetic_input *= Mathf.Clamp01(velocity.magnitude / max_speed);
        }

        // Send synthetic input to ping pong target for animation
        // Note: This is a simplified approach. In practice, you'd need to modify
        // PingPongMovement to accept direct input values or create a CPU-specific version
    }

    // Public API for CPUPlayer to set movement targets
    public void SetTargetPosition(Vector2 target)
    {
        // Limit target distance to prevent extreme movements
        Vector2 current_pos = transform.position;
        Vector2 target_diff = target - current_pos;
        
        if (target_diff.magnitude > max_movement_distance)
        {
            target = current_pos + (target_diff.normalized * max_movement_distance);
        }

        current_target_position = target;
        has_target = true;
    }

    public void ClearTarget()
    {
        has_target = false;
    }

    // Getters for other systems
    public Vector2 CurrentTarget => current_target_position;
    public bool HasTarget => has_target;
    public float DistanceToTarget
    {
        get
        {
            if (!has_target) return 0f;
            return Vector2.Distance(transform.position, current_target_position);
        }
    }

#if UNITY_6000_0_OR_NEWER
    public Vector2 CurrentVelocity => rigidbody2d.linearVelocity;
#else
    public Vector2 CurrentVelocity => rigidbody2d.velocity;
#endif

    // Allow runtime adjustment of parameters
    public void SetMovementParameters(float newMaxSpeed, float newAcceleration, float newDeceleration)
    {
        max_speed = Mathf.Max(0f, newMaxSpeed);
        acceleration = Mathf.Max(0f, newAcceleration);
        deceleration = Mathf.Max(0f, newDeceleration);
    }

    public void SetMovementSmoothing(float smoothing)
    {
        movement_smoothing = Mathf.Clamp01(smoothing);
    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (!has_target) return;

        // Draw target position
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(current_target_position, 0.3f);

        // Draw smoothed target
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(smoothed_target_position, 0.2f);

        // Draw line to target
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, current_target_position);

        // Draw target threshold
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(current_target_position, target_threshold);
    }
}