using UnityEngine;

public class ShapeSpawner : MonoBehaviour
{
    public ShapePool pool;
    public float spawnInterval = 1f;
    public float spawnXRange = 8f;
    public float spawnY = 5f;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            Shape shape = pool.GetFromPool();
            if (shape != null)
            {
                shape.transform.position = new Vector3(Random.Range(-spawnXRange, spawnXRange), spawnY, 0f);
            }
        }
    }
}