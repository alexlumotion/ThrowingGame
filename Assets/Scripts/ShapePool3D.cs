using System.Collections.Generic;
using UnityEngine;

public class ShapePool3D : MonoBehaviour
{
    public GameObject[] shapePrefabs;
    public int poolSizePerType = 5;

    private Dictionary<GameObject, Queue<Shape3D>> pools = new Dictionary<GameObject, Queue<Shape3D>>();

    void Start()
    {
        foreach (var prefab in shapePrefabs)
        {
            Queue<Shape3D> queue = new Queue<Shape3D>();
            for (int i = 0; i < poolSizePerType; i++)
            {
                GameObject obj = Instantiate(prefab);
                obj.SetActive(false);
                Shape3D shape = obj.GetComponent<Shape3D>();
                shape.Init(this);
                queue.Enqueue(shape);
            }
            pools[prefab] = queue;
        }
    }

    public Shape3D GetFromPool()
    {
        if (shapePrefabs.Length == 0) return null;

        GameObject randomPrefab = shapePrefabs[Random.Range(0, shapePrefabs.Length)];
        var pool = pools[randomPrefab];

        if (pool.Count > 0)
        {
            Shape3D shape = pool.Dequeue();
            shape.transform.localScale = new Vector3(3f, 3f, 3f);
            //shape.transform.localScale = Vector3.one;
            shape.gameObject.SetActive(true);
            return shape;
        }

        return null;
    }

    public void ReturnToPool(Shape3D shape)
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