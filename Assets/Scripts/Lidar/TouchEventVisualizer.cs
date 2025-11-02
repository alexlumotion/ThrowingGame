using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listens for JSON touch events from a <see cref="TouchEventClient"/> and visualises
/// each touch_start as a transient marker within a configurable area.
/// </summary>
[RequireComponent(typeof(TouchEventClient))]
public class TouchEventVisualizer : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private TouchEventClient client;

    [Tooltip("Origin transform that defines the centre of the touch area (defaults to this transform).")]
    [SerializeField] private Transform areaOrigin;

    [Tooltip("Size of the touch area in world units (X = width, Y = depth).")]
    [SerializeField] private Vector2 areaSize = new Vector2(1f, 1f);

    [Header("Marker")]
    [Tooltip("Optional prefab used for the touch marker (created at runtime if omitted).")]
    [SerializeField] private GameObject markerPrefab;

    [Tooltip("Uniform scale applied to each created marker (prefab or generated).")]
    [SerializeField] private float markerScale = 0.1f;

    [Tooltip("Colour of the marker while a touch is active.")]
    [SerializeField] private Color activeColor = Color.cyan;

    [Tooltip("Colour the marker fades towards over its lifetime.")]
    [SerializeField] private Color fadedColor = new Color(0f, 1f, 1f, 0f);

    [Tooltip("Lifetime of each spawned marker in seconds.")]
    [SerializeField] private float markerLifetime = 0.5f;

    private readonly List<MarkerInstance> activeMarkers = new List<MarkerInstance>(32);

    [Serializable]
    private class TouchEventPayload
    {
        public string @event;
        public float x;
        public float y;
        public int points;
        public double timestamp;
        public TouchPoint[] touches;
    }

    [Serializable]
    private class TouchPoint
    {
        public float x;
        public float y;
        public int index;
    }

    private sealed class MarkerInstance
    {
        public GameObject GameObject;
        public Renderer Renderer;
        public float RemainingLifetime;
    }

    private void Reset()
    {
        client = GetComponent<TouchEventClient>();
    }

    private void Awake()
    {
        if (client == null)
        {
            client = GetComponent<TouchEventClient>();
        }

        if (areaOrigin == null)
        {
            areaOrigin = transform;
        }
    }

    private void OnEnable()
    {
        if (client != null)
        {
            client.onTouchEventReceived.AddListener(OnTouchEventReceived);
        }
    }

    private void OnDisable()
    {
        if (client != null)
        {
            client.onTouchEventReceived.RemoveListener(OnTouchEventReceived);
        }

        ClearMarkers();
    }

    private void Update()
    {
        if (activeMarkers.Count == 0)
        {
            return;
        }

        float delta = Time.deltaTime;

        for (int i = activeMarkers.Count - 1; i >= 0; i--)
        {
            MarkerInstance marker = activeMarkers[i];
            if (marker == null)
            {
                activeMarkers.RemoveAt(i);
                continue;
            }

            marker.RemainingLifetime -= delta;

            float t = markerLifetime > 0f ? Mathf.Clamp01(1f - (marker.RemainingLifetime / markerLifetime)) : 1f;
            ApplyMarkerColor(marker, Color.Lerp(activeColor, fadedColor, t));

            if (marker.RemainingLifetime <= 0f)
            {
                DestroyMarkerInstance(i);
            }
        }
    }

    /// <summary>
    /// UnityEvent callback wired from <see cref="TouchEventClient.onTouchEventReceived"/>.
    /// </summary>
    /// <param name="json">Raw JSON payload describing the touch event.</param>
    public void OnTouchEventReceived(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        TouchEventPayload payload;
        try
        {
            payload = JsonUtility.FromJson<TouchEventPayload>(json);
        }
        catch (ArgumentException)
        {
            Debug.LogWarning($"[TouchVisualizer] Unable to parse payload: {json}");
            return;
        }

        if (payload == null || string.IsNullOrEmpty(payload.@event))
        {
            return;
        }

        if (!payload.@event.Equals("touch_start", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SpawnMarkersFromPayload(payload);
    }

    private void SpawnMarkersFromPayload(TouchEventPayload payload)
    {
        if (payload.touches != null && payload.touches.Length > 0)
        {
            foreach (var touch in payload.touches)
            {
                SpawnMarker(touch.x, touch.y);
            }
            return;
        }

        SpawnMarker(payload.x, payload.y);
    }

    private void SpawnMarker(float x, float y)
    {
        GameObject markerObject = CreateMarkerGameObject();
        if (markerObject == null)
        {
            return;
        }

        markerObject.transform.position = ProjectToWorld(x, y);

        Renderer renderer = markerObject.GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning("[TouchVisualizer] Marker has no Renderer component; colour fading will be skipped.");
        }

        var markerInstance = new MarkerInstance
        {
            GameObject = markerObject,
            Renderer = renderer,
            RemainingLifetime = markerLifetime
        };

        ApplyMarkerColor(markerInstance, activeColor);
        activeMarkers.Add(markerInstance);
    }

    private GameObject CreateMarkerGameObject()
    {
        GameObject instance;
        Transform parent = areaOrigin != null ? areaOrigin : transform;

        if (markerPrefab != null)
        {
            instance = Instantiate(markerPrefab, parent);
        }
        else
        {
            instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            instance.transform.SetParent(parent, worldPositionStays: false);
#if UNITY_EDITOR
            instance.name = "Touch Marker (auto)";
#endif
            var collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        instance.transform.localScale = Vector3.one * markerScale;
        return instance;
    }

    private Vector3 ProjectToWorld(float x, float y)
    {
        Vector3 localOffset = new Vector3(
            (x - 0.5f) * areaSize.x,
            0f,
            (y - 0.5f) * areaSize.y
        );

        return areaOrigin != null ? areaOrigin.TransformPoint(localOffset) : transform.TransformPoint(localOffset);
    }

    private void ApplyMarkerColor(MarkerInstance marker, Color color)
    {
        if (marker.Renderer != null && marker.Renderer.material != null)
        {
            marker.Renderer.material.color = color;
        }
    }

    private void DestroyMarkerInstance(int index)
    {
        MarkerInstance marker = activeMarkers[index];
        if (marker != null && marker.GameObject != null)
        {
            Destroy(marker.GameObject);
        }

        activeMarkers.RemoveAt(index);
    }

    private void ClearMarkers()
    {
        for (int i = 0; i < activeMarkers.Count; i++)
        {
            MarkerInstance marker = activeMarkers[i];
            if (marker != null && marker.GameObject != null)
            {
                Destroy(marker.GameObject);
            }
        }

        activeMarkers.Clear();
    }
}
