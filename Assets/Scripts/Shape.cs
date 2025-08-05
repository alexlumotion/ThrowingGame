using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Shape : MonoBehaviour
{
    private ShapePool pool;
    private Vector3 startScale;
    private Rigidbody2D rb;

    public float torqueRange = 50f; // нове поле для випадкового обертання

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
            rb.AddTorque(Random.Range(-torqueRange, torqueRange));
        }
    }

    void Update()
    {
        if (transform.position.y < -6f)
        {
            pool.ReturnToPool(this);
        }
    }

    void OnMouseDown()
    {
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
} 