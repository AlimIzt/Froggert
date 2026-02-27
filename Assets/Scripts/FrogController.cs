using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class FrogController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody body;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Collider bodyCollider;
    [SerializeField] private Transform modelRoot;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference crouchAction;
    [SerializeField] private InputActionReference sprintAction;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float groundCheckOffset = 0.15f;
    [SerializeField] private float groundSnapVelocity = -2f;

    [Header("Movement")]
    [SerializeField] private float moveAcceleration = 24f;
    [SerializeField] private float airAcceleration = 10f;
    [SerializeField] private float maxGroundSpeed = 8f;
    [SerializeField] private float maxAirSpeed = 7f;
    [SerializeField] private float speedClampStrength = 12f;
    [SerializeField] private float brakingDrag = 6f;
    [SerializeField] private float coastDrag = 1.5f;
    [SerializeField] private float coastTime = 0.2f;
    [SerializeField] private float airDrag = 0.05f;
    [SerializeField] private float gravityMultiplier = 10f;
    [SerializeField] private float additionalFallAcceleration = 20f;

    [Header("Jump Charge")]
    [SerializeField] private float minJumpForce = 6f;
    [SerializeField] private float maxJumpForce = 13f;
    [SerializeField] private float maxChargeTime = 0.7f;
    [SerializeField] private float jumpForwardBoost = 2.5f;

    [Header("Jump Momentum Chain")]
    [SerializeField] private float chainResetTime = 1.1f;
    [SerializeField] private int maxChain = 3;
    [SerializeField] private float chainJumpBonus = 0.2f;
    [SerializeField] private float chainSpeedBonus = 0.1f;

    [Header("Wall Jump")]
    [SerializeField] private float wallCheckDistance = 0.7f;
    [SerializeField] private float wallCheckRadius = 0.25f;
    [SerializeField] private float wallJumpUpForce = 7f;
    [SerializeField] private float wallJumpAwayForce = 6f;
    [SerializeField] private float wallJumpGraceTime = 0.2f;
    [SerializeField] private float wallStickForce = 45f;
    [SerializeField] private float wallClingJumpForce = 9f;

    [Header("Slide")]
    [SerializeField] private float slideMinSpeed = 5f;
    [SerializeField] private float slideAcceleration = 8f;
    [SerializeField] private float slideDrag = 0.4f;
    [SerializeField] private PhysicsMaterial slideMaterial;

    [Header("Roll")]
    [SerializeField] private float rollTorque = 30f;
    [SerializeField] private float rollSpeedMultiplier = 1.2f;
    [SerializeField] private PhysicsMaterial rollMaterial;

    [Header("Materials")]
    [SerializeField] private PhysicsMaterial normalMaterial;

    private Vector2 moveInput;
    private bool crouchHeld;
    private bool sprintHeld;
    private bool jumpHeld;
    private bool isChargingJump;
    private float jumpChargeTimer;
    private bool grounded;
    private int jumpChain;
    private float lastJumpTime;
    private float lastMoveInputTime;
    private float lastWallContactTime = float.NegativeInfinity;
    private Vector3 lastWallNormal = Vector3.zero;
    private bool airJumpUsed;

    private bool sliding;
    private bool rolling;
    private bool wallClinging;
    private Vector3 wallClingNormal = Vector3.zero;
    private bool externalDampingOverride;
    private float externalDampingValue;

    private void Reset()
    {
        body = GetComponent<Rigidbody>();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (body != null)
        {
            // Ensure physics-driven movement is enabled.
            body.isKinematic = false;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
        }

    }

    private void OnEnable()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (moveAction != null) moveAction.action.Enable();
        if (jumpAction != null)
        {
            jumpAction.action.Enable();
            jumpAction.action.started += OnJumpStarted;
            jumpAction.action.canceled += OnJumpCanceled;
        }
        if (crouchAction != null) crouchAction.action.Enable();
        if (sprintAction != null) sprintAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (jumpAction != null)
        {
            jumpAction.action.started -= OnJumpStarted;
            jumpAction.action.canceled -= OnJumpCanceled;
            jumpAction.action.Disable();
        }
        if (crouchAction != null) crouchAction.action.Disable();
        if (sprintAction != null) sprintAction.action.Disable();
    }

    private void Update()
    {
        if (moveAction != null)
        {
            moveInput = moveAction.action.ReadValue<Vector2>();
        }
        if (moveInput.sqrMagnitude > 0.01f)
        {
            lastMoveInputTime = Time.time;
        }

        if (Time.time - lastJumpTime > chainResetTime)
        {
            jumpChain = 0;
        }

        crouchHeld = crouchAction != null && crouchAction.action.IsPressed();
        sprintHeld = sprintAction != null && sprintAction.action.IsPressed();
        jumpHeld = jumpAction != null && jumpAction.action.IsPressed();

        if (isChargingJump)
        {
            jumpChargeTimer += Time.deltaTime;
            if (jumpChargeTimer > maxChargeTime)
            {
                jumpChargeTimer = maxChargeTime;
            }
        }
    }

    private void FixedUpdate()
    {
        UpdateGrounding();
        UpdateWallContact();
        UpdateModes();
        UpdateWallCling();
        if (wallClinging)
        {
            ApplyWallCling();
            return;
        }
        ApplyMovement();
        ApplySlideAndRoll();
        ApplyExtraGravity();
    }

    private void UpdateGrounding()
    {
        Vector3 origin = groundCheck != null
            ? groundCheck.position
            : body.position + Vector3.up * groundCheckOffset;
        grounded = Physics.CheckSphere(origin, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

        if (grounded)
        {
            airJumpUsed = false;
            if (wallClinging)
            {
                wallClinging = false;
                body.useGravity = true;
            }
        }

        if (grounded && body.linearVelocity.y < groundSnapVelocity)
        {
            Vector3 velocity = body.linearVelocity;
            velocity.y = groundSnapVelocity;
            body.linearVelocity = velocity;
        }
    }

    private void UpdateWallContact()
    {
        if (grounded)
        {
            lastWallContactTime = float.NegativeInfinity;
            lastWallNormal = Vector3.zero;
            return;
        }

        Vector3 direction = GetMoveDirection();
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = body.linearVelocity.normalized;
        }

        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        if (Physics.SphereCast(body.position, wallCheckRadius, direction, out RaycastHit hit, wallCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            lastWallNormal = hit.normal;
            lastWallContactTime = Time.time;
        }
    }

    private void UpdateModes()
    {
        float horizontalSpeed = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z).magnitude;
        bool wantsSlide = grounded && crouchHeld && horizontalSpeed >= slideMinSpeed;
        sliding = wantsSlide;
        rolling = grounded && sprintHeld && !crouchHeld;

        if (bodyCollider != null)
        {
            if (sliding && slideMaterial != null)
            {
                bodyCollider.material = slideMaterial;
            }
            else if (rolling && rollMaterial != null)
            {
                bodyCollider.material = rollMaterial;
            }
            else if (normalMaterial != null)
            {
                bodyCollider.material = normalMaterial;
            }
        }
    }

    private void ApplyMovement()
    {
        Vector3 moveDirection = GetMoveDirection();
        float acceleration = grounded ? moveAcceleration : airAcceleration;
        float speedMultiplier = GetChainSpeedMultiplier() * (rolling ? rollSpeedMultiplier : 1f);
        float maxSpeed = (grounded ? maxGroundSpeed : maxAirSpeed) * speedMultiplier;

        if (moveDirection.sqrMagnitude > 0.01f && !sliding)
        {
            body.AddForce(moveDirection * acceleration, ForceMode.Acceleration);
            body.linearDamping = grounded ? 0f : airDrag;
        }
        else
        {
            if (grounded && Time.time - lastMoveInputTime <= coastTime)
            {
                body.linearDamping = coastDrag;
            }
            else
            {
                body.linearDamping = grounded ? brakingDrag : airDrag;
            }
        }

        Vector3 horizontalVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        if (horizontalVelocity.magnitude > maxSpeed)
        {
            Vector3 excess = horizontalVelocity - horizontalVelocity.normalized * maxSpeed;
            body.AddForce(-excess * speedClampStrength, ForceMode.Acceleration);
        }

        if (externalDampingOverride)
        {
            body.linearDamping = externalDampingValue;
        }
    }

    private void ApplySlideAndRoll()
    {
        if (sliding)
        {
            Vector3 forward = GetMoveDirection();
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z).normalized;
            }

            if (forward.sqrMagnitude > 0.01f)
            {
                body.AddForce(forward * slideAcceleration, ForceMode.Acceleration);
            }

            body.linearDamping = slideDrag;
        }

        if (rolling)
        {
            Vector3 forward = GetMoveDirection();
            if (forward.sqrMagnitude > 0.01f)
            {
                Vector3 torque = Vector3.Cross(Vector3.up, forward) * rollTorque;
                body.AddTorque(torque, ForceMode.Acceleration);
            }
        }
    }

    private void ApplyExtraGravity()
    {
        if (grounded)
        {
            return;
        }

        float extraGravity = Mathf.Max(0f, gravityMultiplier - 1f) * Physics.gravity.magnitude;
        float totalExtra = extraGravity + Mathf.Max(0f, additionalFallAcceleration);
        if (totalExtra > 0f)
        {
            body.AddForce(Vector3.down * totalExtra, ForceMode.Acceleration);
        }
    }

    public void SetExternalLinearDampingOverride(bool enabled, float value)
    {
        externalDampingOverride = enabled;
        externalDampingValue = value;
    }


    private Vector3 GetMoveDirection()
    {
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        if (input.sqrMagnitude < 0.01f)
        {
            return Vector3.zero;
        }

        if (cameraTransform == null)
        {
            return input.normalized;
        }

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = cameraTransform.right;
        right.y = 0f;
        right.Normalize();
        return (forward * input.z + right * input.x).normalized;
    }

    private void OnJumpStarted(InputAction.CallbackContext context)
    {
        if (grounded)
        {
            isChargingJump = true;
            jumpChargeTimer = 0f;
        }
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        if (wallClinging)
        {
            PerformWallClingJump();
            wallClinging = false;
            body.useGravity = true;
            airJumpUsed = true;
            return;
        }

        if (isChargingJump)
        {
            PerformChargedJump();
        }
    }

    private void PerformChargedJump()
    {
        isChargingJump = false;

        float chargePercent = maxChargeTime <= 0f ? 1f : Mathf.Clamp01(jumpChargeTimer / maxChargeTime);
        float baseJumpForce = Mathf.Lerp(minJumpForce, maxJumpForce, chargePercent);
        float jumpForce = baseJumpForce * GetChainJumpMultiplier();

        Vector3 velocity = body.linearVelocity;
        if (velocity.y < 0f)
        {
            velocity.y = 0f;
            body.linearVelocity = velocity;
        }

        Vector3 jumpDirection = Vector3.up * jumpForce;
        Vector3 forward = GetMoveDirection();
        if (forward.sqrMagnitude > 0.01f)
        {
            jumpDirection += forward * jumpForwardBoost;
        }

        body.AddForce(jumpDirection, ForceMode.Impulse);
        RegisterJump();
    }

    private void PerformWallJump()
    {
        Vector3 velocity = body.linearVelocity;
        if (velocity.y < 0f)
        {
            velocity.y = 0f;
            body.linearVelocity = velocity;
        }

        Vector3 jump = Vector3.up * wallJumpUpForce + lastWallNormal * wallJumpAwayForce;
        body.AddForce(jump, ForceMode.Impulse);
        RegisterJump();
    }

    private void PerformWallClingJump()
    {
        Vector3 normal = wallClingNormal;
        if (normal.sqrMagnitude < 0.01f)
        {
            normal = lastWallNormal;
        }
        if (normal.sqrMagnitude < 0.01f && cameraTransform != null)
        {
            normal = -cameraTransform.forward;
            normal.y = 0f;
            normal.Normalize();
        }

        Vector3 direction = (normal + Vector3.up).normalized;
        body.AddForce(direction * wallClingJumpForce, ForceMode.Impulse);
        RegisterJump();
    }

    private void UpdateWallCling()
    {
        if (grounded)
        {
            return;
        }

        bool nearWall = Time.time - lastWallContactTime <= wallJumpGraceTime && lastWallNormal.sqrMagnitude > 0.01f;
        if (wallClinging)
        {
            if (!jumpHeld)
            {
                wallClinging = false;
                body.useGravity = true;
            }
            return;
        }

        if (jumpHeld && nearWall)
        {
            wallClinging = true;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            wallClingNormal = ResolveWallNormal();
            if (wallClingNormal.sqrMagnitude < 0.01f)
            {
                wallClingNormal = lastWallNormal;
            }
        }
        else if (wallClinging)
        {
            wallClinging = false;
            body.useGravity = true;
        }
    }

    private void ApplyWallCling()
    {
        body.linearVelocity = Vector3.zero;
        body.AddForce(-lastWallNormal * wallStickForce, ForceMode.Acceleration);
    }

    private Vector3 ResolveWallNormal()
    {
        Collider[] hits = Physics.OverlapSphere(
            body.position,
            wallCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        Vector3 bestNormal = Vector3.zero;

        foreach (Collider hit in hits)
        {
            Vector3 closest = hit.ClosestPoint(body.position);
            float distance = Vector3.Distance(body.position, closest);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                Vector3 normal = (body.position - closest);
                normal.y = 0f;
                if (normal.sqrMagnitude > 0.001f)
                {
                    bestNormal = normal.normalized;
                }
            }
        }

        return bestNormal;
    }

    private void RegisterJump()
    {
        if (Time.time - lastJumpTime <= chainResetTime)
        {
            jumpChain = Mathf.Min(jumpChain + 1, maxChain);
        }
        else
        {
            jumpChain = 0;
        }

        lastJumpTime = Time.time;
    }

    private float GetChainJumpMultiplier()
    {
        return 1f + jumpChain * chainJumpBonus;
    }

    private float GetChainSpeedMultiplier()
    {
        return 1f + jumpChain * chainSpeedBonus;
    }
}

