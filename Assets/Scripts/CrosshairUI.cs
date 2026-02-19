using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image crosshairImage;

    [Header("Style")]
    [SerializeField] private Color color = Color.white;
    [SerializeField] private float size = 18f;

    private void Awake()
    {
        EnsureCanvas();
        EnsureCrosshair();
        ApplyStyle();
    }

    private void OnValidate()
    {
        ApplyStyle();
    }

    private void EnsureCanvas()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("CrosshairCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private void EnsureCrosshair()
    {
        if (crosshairImage != null)
        {
            return;
        }

        GameObject imageObject = new GameObject("Crosshair");
        imageObject.transform.SetParent(canvas.transform, false);
        crosshairImage = imageObject.AddComponent<Image>();
        crosshairImage.raycastTarget = false;

        RectTransform rect = crosshairImage.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    private void ApplyStyle()
    {
        if (crosshairImage == null)
        {
            return;
        }

        crosshairImage.color = color;
        crosshairImage.rectTransform.sizeDelta = new Vector2(size, size);
    }
}

