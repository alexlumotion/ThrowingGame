using System;
using UnityEngine;

/// <summary>
/// Приймає дані з TouchEventClient та перетворює їх у екранні координати для ClickRouter.
/// </summary>
public class LidarClickBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TouchEventClient touchClient;
    [SerializeField] private ClickRouter clickRouter;
    [Tooltip("Необов'язково: якщо не задано, використаємо камеру з ClickRouter або Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Header("Filter")]
    [Tooltip("Який тип події обробляти (наприклад, touch_end). Порожній рядок — приймати всі.")]
    [SerializeField] private string acceptedEventType = "touch_end";

    private void Awake()
    {
        if (!touchClient)
            touchClient = GetComponent<TouchEventClient>();

        if (!clickRouter)
            clickRouter = GetComponent<ClickRouter>();
    }

    private void OnEnable()
    {
        if (touchClient != null)
        {
            touchClient.TouchEventReceived += HandleTouchEvent;
        }
        else
        {
            Debug.LogWarning("[LidarClickBridge] TouchEventClient reference is missing.");
        }
    }

    private void OnDisable()
    {
        if (touchClient != null)
            touchClient.TouchEventReceived -= HandleTouchEvent;
    }

    private void HandleTouchEvent(string json)
    {
        if (clickRouter == null)
        {
            Debug.LogWarning("[LidarClickBridge] ClickRouter reference is missing.");
            return;
        }

        var cam = ResolveCamera();
        if (cam == null)
        {
            Debug.LogWarning("[LidarClickBridge] Unable to resolve target Camera.");
            return;
        }

        TouchPayload payload;
        try
        {
            payload = JsonUtility.FromJson<TouchPayload>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LidarClickBridge] Failed to parse touch payload: {ex.Message}\n{json}");
            return;
        }

        if (payload == null)
            return;

        if (!string.IsNullOrEmpty(acceptedEventType) && !string.Equals(payload.@event, acceptedEventType, StringComparison.OrdinalIgnoreCase))
            return;

        float normalizedY = Mathf.Clamp01(payload.y + 0.5f); // -0.5..0.5 -> 0..1 (X on screen)
        float normalizedX = Mathf.Clamp01(payload.x);         // 0..1 -> 0..1 (Y on screen)

        float screenX = normalizedY * cam.pixelWidth;
        float screenY = normalizedX * cam.pixelHeight;

        clickRouter.RouteScreenPoint(new Vector2(screenX, screenY));
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        if (clickRouter != null && clickRouter.cam != null)
            return clickRouter.cam;

        return Camera.main;
    }

    [Serializable]
    private class TouchPayload
    {
        public string @event;
        public float x;
        public float y;
        public double timestamp;
    }
}
