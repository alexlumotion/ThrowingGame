// === Shape.cs ===
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Shape : MonoBehaviour
{
    private ShapePool pool;
    private Vector3 startScale;
    private Rigidbody2D rb;

    public float torqueRange = 50f;

    public void Init(ShapePool shapePool)
    {
        pool = shapePool;
        startScale = Vector3.one;
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        transform.localScale = startScale;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 1f;
            rb.drag = 0f;
            rb.angularDrag = 0.05f;
            rb.AddTorque(Random.Range(-torqueRange, torqueRange));
        }
    }

    void Update()
    {
        if (transform.position.y < -6f || Mathf.Abs(transform.position.x) > 20f || transform.position.y > 20f)
        {
            pool.ReturnToPool(this);
        }
    }

    void OnMouseDown()
    {
        GameObject fx = ParticlePool.Instance.GetFromPool();
        fx.transform.position = transform.position;
        fx.GetComponent<ParticleSystem>().Play();
        ParticlePool.Instance.ReturnToPoolDelayed(fx, 2f);

        StartCoroutine(Disappear());
    }

    private System.Collections.IEnumerator Disappear()
    {
        float t = 0f;
        Vector3 currentScale = transform.localScale;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(currentScale, Vector3.zero, t / 0.2f);
            yield return null;
        }
        pool.ReturnToPool(this);
    }

    public void SetTransform(float spawnXRange, float spawnY)
    {
        transform.position = new Vector3(Random.Range(-spawnXRange, spawnXRange), spawnY, 0f);

        if (rb != null)
        {
            rb.gravityScale = 1f;
            rb.drag = 0f;
            rb.angularDrag = 0.05f;
        }
    }

    public void LaunchFromOffscreen(Vector2 spawnPoint, Vector2 targetPoint, float speed)
    {
        transform.position = spawnPoint;
        Vector2 direction = (targetPoint - spawnPoint).normalized;

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.drag = 0f;
            rb.angularDrag = 0.05f;
            rb.velocity = direction * speed;
        }

        transform.up = direction;
    }
}