using System.Collections.Generic;
using UnityEngine;

public class ShapePool : MonoBehaviour
{
    public GameObject[] shapePrefabs;
    public int poolSizePerType = 5;

    private Dictionary<GameObject, Queue<Shape>> pools = new Dictionary<GameObject, Queue<Shape>>();

    void Start()
    {
        foreach (var prefab in shapePrefabs)
        {
            Queue<Shape> queue = new Queue<Shape>();
            for (int i = 0; i < poolSizePerType; i++)
            {
                GameObject obj = Instantiate(prefab);
                obj.SetActive(false);
                Shape shape = obj.GetComponent<Shape>();
                shape.Init(this);
                queue.Enqueue(shape);
            }
            pools[prefab] = queue;
        }
    }

    public Shape GetFromPool()
    {
        if (shapePrefabs.Length == 0) return null;

        GameObject randomPrefab = shapePrefabs[Random.Range(0, shapePrefabs.Length)];
        var pool = pools[randomPrefab];

        if (pool.Count > 0)
        {
            Shape shape = pool.Dequeue();
            shape.transform.localScale = Vector3.one;
            shape.gameObject.SetActive(true);
            return shape;
        }

        return null;
    }

    public void ReturnToPool(Shape shape)
    {
        shape.gameObject.SetActive(false);
        foreach (var kvp in pools)
        {
            if (kvp.Key.name == shape.gameObject.name.Replace("(Clone)", ""))
            {
                kvp.Value.Enqueue(shape);
                return;
            }
        }
    }
}