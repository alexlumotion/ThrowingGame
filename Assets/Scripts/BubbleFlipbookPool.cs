using System.Collections.Generic;
using UnityEngine;

public class BubbleFlipbookPool : MonoBehaviour
{
    public static BubbleFlipbookPool Instance { get; private set; }
    public GameObject flipbookPrefab;
    public int poolSize = 10;

    private Queue<GameObject> pool = new Queue<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(flipbookPrefab, transform);
            pool.Enqueue(obj);
        }
    }

    public GameObject GetFromPool()
    {
        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        else
        {
            // якщо всі зайняті — можна або створити новий, або повернути null
            GameObject obj = Instantiate(flipbookPrefab, transform);
            return obj;
        }
    }

    public void ReturnToPool(GameObject obj)
    {
        obj.transform.position = new(20f, 0f, 0f);
        pool.Enqueue(obj);
    }

    public void StartFlipbook(Vector3 targetPosition)
    {
        GameObject target = GetFromPool();
        target.transform.position = new Vector3(targetPosition.x, targetPosition.y, targetPosition.z - 1);
        target.GetComponent<ShellFlipbook>().PlayOnce();
    }

}