using UnityEngine;

public class PlayerDebugUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody body;

    [Header("Display")]
    [SerializeField] private bool showDebug = true;
    [SerializeField] private int fontSize = 14;
    [SerializeField] private Vector2 screenOffset = new Vector2(12f, 140f);

    private Vector3 lastVelocity;
    private Vector3 estimatedForce;

    private void Reset()
    {
        body = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (body != null)
        {
            lastVelocity = body.linearVelocity;
        }
    }

    private void FixedUpdate()
    {
        if (body == null)
        {
            return;
        }

        float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        Vector3 currentVelocity = body.linearVelocity;
        Vector3 acceleration = (currentVelocity - lastVelocity) / dt;
        estimatedForce = acceleration * body.mass;
        lastVelocity = currentVelocity;
    }

    private void OnGUI()
    {
        if (!showDebug || body == null)
        {
            return;
        }

        float speed = body.linearVelocity.magnitude;
        Vector3 horizontal = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        float horizontalSpeed = horizontal.magnitude;
        float momentum = body.mass * speed;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize
        };

        string text =
            $"Player Debug\n" +
            $"Speed: {speed:F2} m/s\n" +
            $"Horizontal Speed: {horizontalSpeed:F2} m/s\n" +
            $"Momentum: {momentum:F2} kg·m/s\n" +
            $"Force (est): {estimatedForce.magnitude:F2} N\n" +
            $"Force Vec: {estimatedForce}";

        GUI.Label(new Rect(screenOffset.x, screenOffset.y, 360f, 140f), text, style);
    }
}

