// === ShapeSpawner.cs ===
using UnityEngine;

public class ShapeSpawner : MonoBehaviour
{
    public ShapePool pool;
    public float spawnInterval = 1f;
    public float spawnXRange = 8f;
    public float spawnY = 5f;
    public bool useFlightMode = false;
    public float flightSpeed = 5f;

    private float timer;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            Shape shape = pool.GetFromPool();
            if (shape != null)
            {
                if (useFlightMode)
                {
                    Vector2 spawn = GetRandomOffscreenPoint();
                    Vector2 target = GetRandomPointInsideScreen();
                    shape.LaunchFromOffscreen(spawn, target, flightSpeed);
                }
                else
                {
                    shape.SetTransform(spawnXRange, spawnY);
                }
            }
        }
    }

    Vector2 GetRandomOffscreenPoint()
    {
        float camHeight = 2f * mainCamera.orthographicSize;
        float camWidth = camHeight * mainCamera.aspect;
        float offset = 1f;

        int side = Random.Range(0, 4); // 0 = left, 1 = right, 2 = top, 3 = bottom
        switch (side)
        {
            case 0: return new Vector2(-camWidth / 2 - offset, Random.Range(-camHeight / 2, camHeight / 2));
            case 1: return new Vector2(camWidth / 2 + offset, Random.Range(-camHeight / 2, camHeight / 2));
            case 2: return new Vector2(Random.Range(-camWidth / 2, camWidth / 2), camHeight / 2 + offset);
            case 3: return new Vector2(Random.Range(-camWidth / 2, camWidth / 2), -camHeight / 2 - offset);
        }
        return Vector2.zero;
    }

    Vector2 GetRandomPointInsideScreen()
    {
        float camHeight = 2f * mainCamera.orthographicSize;
        float camWidth = camHeight * mainCamera.aspect;
        return new Vector2(Random.Range(-camWidth / 2, camWidth / 2), Random.Range(-camHeight / 2, camHeight / 2));
    }
}