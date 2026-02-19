using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Distance")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 8f;

    [Header("Rotation")]
    [SerializeField] private float yawSpeed = 180f;
    [SerializeField] private float pitchSpeed = 140f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private bool invertY;
    [SerializeField] private float stickSensitivity = 1f;
    [SerializeField] private float mouseSensitivity = 1.0f;
    [SerializeField] private bool useDeltaTimeForMouse = true;
    [SerializeField] private bool useRawMouseInput = true;

    [Header("Shoulder")]
    [SerializeField] private Vector3 shoulderOffset = new Vector3(0.35f, 0.1f, 0f);

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.08f;
    [SerializeField] private float rotationSmoothTime = 0.05f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float collisionPadding = 0.1f;

    [Header("Input")]
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private bool lockCursorOnStart = true;

    private Vector2 lookInput;
    private float yaw;
    private float pitch;
    private float yawVelocity;
    private float pitchVelocity;
    private Vector3 positionVelocity;

    private void OnEnable()
    {
        if (lookAction != null)
        {
            lookAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (lookAction != null)
        {
            lookAction.action.Disable();
        }
    }

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (useRawMouseInput && Mouse.current != null)
        {
            lookInput = Mouse.current.delta.ReadValue();
        }
        else if (lookAction != null)
        {
            lookInput = lookAction.action.ReadValue<Vector2>();
        }

    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float deltaYaw;
        float deltaPitch;
        if (useRawMouseInput && Mouse.current != null)
        {
            // Raw mouse delta is already per-frame, treat sensitivity as degrees per pixel.
            deltaYaw = lookInput.x * mouseSensitivity;
            deltaPitch = lookInput.y * mouseSensitivity;
        }
        else
        {
            float sensitivity = GetLookSensitivity();
            float deltaTimeScale = UsesDeltaTime() ? Time.deltaTime : 1f;
            deltaYaw = lookInput.x * yawSpeed * sensitivity * deltaTimeScale;
            deltaPitch = lookInput.y * pitchSpeed * sensitivity * deltaTimeScale;
        }
        if (invertY)
        {
            deltaPitch = -deltaPitch;
        }

        yaw += deltaYaw;
        pitch = Mathf.Clamp(pitch - deltaPitch, minPitch, maxPitch);


        float smoothYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, yaw, ref yawVelocity, rotationSmoothTime);
        float smoothPitch = Mathf.SmoothDampAngle(transform.eulerAngles.x, pitch, ref pitchVelocity, rotationSmoothTime);

        Quaternion rotation = Quaternion.Euler(smoothPitch, smoothYaw, 0f);

        float clampedDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        Vector3 focusPoint = target.position + targetOffset;
        Vector3 desiredPosition = focusPoint + rotation * shoulderOffset - rotation * Vector3.forward * clampedDistance;

        if (Physics.SphereCast(focusPoint, collisionRadius, (desiredPosition - focusPoint).normalized,
            out RaycastHit hit, clampedDistance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            float hitDistance = Mathf.Max(hit.distance - collisionPadding, minDistance);
            desiredPosition = focusPoint + rotation * shoulderOffset - rotation * Vector3.forward * hitDistance;
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);
        transform.rotation = rotation;
    }

    private float GetLookSensitivity()
    {
        if (lookAction == null || lookAction.action == null || lookAction.action.activeControl == null)
        {
            return stickSensitivity;
        }

        if (useRawMouseInput && Mouse.current != null)
        {
            return mouseSensitivity;
        }

        InputDevice device = lookAction.action.activeControl.device;
        return device is Mouse ? mouseSensitivity : stickSensitivity;
    }

    private bool UsesDeltaTime()
    {
        if (useRawMouseInput && Mouse.current != null)
        {
            return false;
        }

        if (lookAction == null || lookAction.action == null || lookAction.action.activeControl == null)
        {
            return true;
        }

        InputDevice device = lookAction.action.activeControl.device;
        if (device is Mouse)
        {
            return useDeltaTimeForMouse;
        }

        return true;
    }
}

