using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class TongueGrapple : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody body;
    [SerializeField] private FrogController frogController;
    [SerializeField] private Transform tongueOrigin;
    [SerializeField] private Transform aimTransform;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Camera aimCamera;

    [Header("Input")]
    [SerializeField] private InputActionReference grappleAction;
    [SerializeField] private InputActionReference moveAction;

    [Header("Grapple Settings")]
    [SerializeField] private LayerMask grappleMask = ~0;
    [SerializeField] private float maxDistance = 18f;
    [SerializeField] private float spring = 120f;
    [SerializeField] private float damper = 7f;
    [SerializeField] private float massScale = 4f;
    [SerializeField] private float minDistanceScale = 0.3f;
    [SerializeField] private float maxDistanceScale = 0.8f;
    [SerializeField] private float swingAcceleration = 16f;
    [SerializeField] private float maxGrappleSpeed = 12f;
    [SerializeField] private float speedClampStrength = 10f;
    [SerializeField] private float grappleLinearDamping = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool debugDraw = true;
    [SerializeField] private bool debugUI = true;
    [SerializeField] private float debugRayTime = 0.15f;

    private SpringJoint joint;
    private Vector3 grapplePoint;
    private Vector2 moveInput;
    private string debugStatus = "Idle";
    private bool dampingOverridden;

    public bool IsGrappling => joint != null;

    private void Reset()
    {
        body = GetComponent<Rigidbody>();
        frogController = GetComponent<FrogController>();
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void OnEnable()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }
        if (frogController == null)
        {
            frogController = GetComponent<FrogController>();
        }

        if (grappleAction != null)
        {
            grappleAction.action.Enable();
            grappleAction.action.started += OnGrappleStarted;
            grappleAction.action.canceled += OnGrappleCanceled;
        }

        if (moveAction != null)
        {
            moveAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (grappleAction != null)
        {
            grappleAction.action.started -= OnGrappleStarted;
            grappleAction.action.canceled -= OnGrappleCanceled;
            grappleAction.action.Disable();
        }

        if (moveAction != null)
        {
            moveAction.action.Disable();
        }

        DisableGrapple();
    }

    private void Update()
    {
        if (moveAction != null)
        {
            moveInput = moveAction.action.ReadValue<Vector2>();
        }

        UpdateLine();
    }

    private void FixedUpdate()
    {
        if (joint == null)
        {
            return;
        }

        Vector3 swingDirection = GetSwingDirection(moveInput);
        if (swingDirection.sqrMagnitude > 0.01f)
        {
            body.AddForce(swingDirection * swingAcceleration, ForceMode.Acceleration);
        }

        float speed = body.linearVelocity.magnitude;
        if (speed > maxGrappleSpeed)
        {
            Vector3 counter = -body.linearVelocity.normalized * (speed - maxGrappleSpeed) * speedClampStrength;
            body.AddForce(counter, ForceMode.Acceleration);
        }
    }

    private void OnGrappleStarted(InputAction.CallbackContext context)
    {
        debugStatus = "Input: Grapple pressed";
        TryStartGrapple();
    }

    private void OnGrappleCanceled(InputAction.CallbackContext context)
    {
        debugStatus = "Input: Grapple released";
        DisableGrapple();
    }

    private void TryStartGrapple()
    {
        Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;
        Vector3 start;
        Vector3 direction;

        if (cameraToUse != null)
        {
            Ray ray = cameraToUse.ScreenPointToRay(new Vector3(
                cameraToUse.pixelWidth * 0.5f,
                cameraToUse.pixelHeight * 0.5f,
                0f));
            start = ray.origin;
            direction = ray.direction.normalized;
        }
        else
        {
            Transform origin = tongueOrigin != null ? tongueOrigin : transform;
            start = origin.position;
            direction = GetAimDirection();
        }

        if (debugDraw)
        {
            Debug.DrawRay(start, direction * maxDistance, Color.yellow, debugRayTime);
        }

        if (Physics.Raycast(start, direction, out RaycastHit hit, maxDistance, grappleMask, QueryTriggerInteraction.Ignore))
        {
            grapplePoint = hit.point;
            joint = gameObject.AddComponent<SpringJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = grapplePoint;
            joint.spring = spring;
            joint.damper = damper;
            joint.massScale = massScale;

            float distance = Vector3.Distance(body.position, grapplePoint);
            joint.minDistance = distance * minDistanceScale;
            joint.maxDistance = distance * maxDistanceScale;
            debugStatus = $"Grapple hit: {hit.collider.name}";

            if (frogController != null)
            {
                frogController.SetExternalLinearDampingOverride(true, grappleLinearDamping);
                dampingOverridden = true;
            }
        }
        else
        {
            debugStatus = "Grapple miss: no hit";
        }
    }

    private void DisableGrapple()
    {
        if (joint != null)
        {
            Destroy(joint);
            joint = null;
            debugStatus = "Grapple released";
        }

        if (dampingOverridden && frogController != null)
        {
            frogController.SetExternalLinearDampingOverride(false, 0f);
            dampingOverridden = false;
        }
    }

    public void ReleaseGrapple()
    {
        DisableGrapple();
    }

    private void UpdateLine()
    {
        if (lineRenderer == null)
        {
            return;
        }

        if (joint == null)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        Transform origin = tongueOrigin != null ? tongueOrigin : transform;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, origin.position);
        lineRenderer.SetPosition(1, grapplePoint);
    }

    private Vector3 GetAimDirection()
    {
        Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;
        if (cameraToUse != null)
        {
            Ray ray = cameraToUse.ScreenPointToRay(new Vector3(
                cameraToUse.pixelWidth * 0.5f,
                cameraToUse.pixelHeight * 0.5f,
                0f));
            return ray.direction.normalized;
        }

        Transform target = aimTransform != null ? aimTransform : transform;
        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }
        return forward.normalized;
    }

    private void OnGUI()
    {
        if (!debugUI)
        {
            return;
        }

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14
        };

        string jointText = joint != null ? "Yes" : "No";
        string cameraText = aimCamera != null ? aimCamera.name : (Camera.main != null ? Camera.main.name : "None");
        string text =
            $"Grapple Debug\n" +
            $"Status: {debugStatus}\n" +
            $"Has Joint: {jointText}\n" +
            $"Aim Camera: {cameraText}\n" +
            $"Mask: {grappleMask.value}\n" +
            $"Max Distance: {maxDistance:F1}";

        GUI.Label(new Rect(12f, 12f, 380f, 120f), text, style);
    }

    private Vector3 GetSwingDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f)
        {
            return Vector3.zero;
        }

        Transform target = aimTransform != null ? aimTransform : transform;
        Vector3 forward = target.forward;
        Vector3 right = target.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        return (forward * input.y + right * input.x).normalized;
    }
}

