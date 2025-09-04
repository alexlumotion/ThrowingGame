// === Shape.cs (3D) ===
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Shape3D : MonoBehaviour
{
    private ShapePool3D pool;
    private Vector3 startScale;
    private Rigidbody rb;

    [Header("Physics")]
    [Tooltip("Максимальна додаткова крутильна швидкість при появі")]
    public float torqueRange = 50f; // у 3D буде застосовано випадковий вектор крутіння

    [Header("Audio")]
    public AudioClip spawnClip;
    public AudioClip destroyClip;
    public AudioClip collisionClip;

    [Header("Collision sound filter")]
    public bool enableCollisionFilter = false;
    public float collisionVelocityThreshold = 1.5f;
    public float collisionSoundCooldown = 0.2f;

    //private AudioSource audioSource;
    private float lastCollisionSoundTime;

    [SerializeField] private Shape3DCollision shape3DCollision;

    public void Init(ShapePool3D shapePool)
    {
        pool = shapePool;
        startScale = new Vector3(3f, 3f, 3f);
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        Debug.Log("OnEnable");

        shape3DCollision.SetOFFAnimation();

        transform.localScale = startScale;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = true;
            rb.drag = 0f;
            rb.angularDrag = 0.05f;

            // випадковий крутний момент в 3D
            Vector3 randomTorque = Random.insideUnitSphere * torqueRange;
            rb.AddTorque(randomTorque, ForceMode.Acceleration);
        }
    }

    void Awake()
    {
        shape3DCollision = GetComponent<Shape3DCollision>();
    }

    void Update()
    {
        // прості межі сцени (z залишаємо біля 0, але можна додати обмеження за потреби)
        if (transform.position.y < -6f ||
            Mathf.Abs(transform.position.x) > 20f ||
            transform.position.y > 20f)
        {
            //shape3DCollision.SetOFFAnimation();
            pool.ReturnToPool(this);
            //shape3DCollision.SetOFFAnimation();
        }
    }

    void OnMouseDown()
    {
        GameObject fx = ParticlePool.Instance.GetFromPool();
        fx.transform.position = transform.position;
        fx.GetComponent<ParticleSystem>().Play();
        ParticlePool.Instance.ReturnToPoolDelayed(fx, 2f);

        //StartCoroutine(Disappear());
        rb.isKinematic = true;
        shape3DCollision.OnClick(() =>
        {
            //Debug.Log("HUY 2");
            //StartCoroutine(Disappear());
            rb.isKinematic = false;
        });
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collisionClip != null /* && audioSource != null */)
        {
            if (enableCollisionFilter)
            {
                float now = Time.time;
                if (now - lastCollisionSoundTime > collisionSoundCooldown &&
                    collision.relativeVelocity.magnitude > collisionVelocityThreshold)
                {
                    lastCollisionSoundTime = now;
                }
            }
            else
            {
                // if (collision.relativeVelocity.magnitude > collisionVelocityThreshold)
                //     audioSource.PlayOneShot(collisionClip);
            }
        }
    }

    private System.Collections.IEnumerator Disappear()
    {
        float t = 0f;
        Vector3 currentScale = transform.localScale;
        const float duration = 0.2f;

        // під час зникнення вимикаємо фізику, щоб не трусило об'єкт
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(currentScale, Vector3.zero, t / duration);
            yield return null;
        }

        if (rb != null) rb.isKinematic = false;
        pool.ReturnToPool(this);
    }

    // Спавн «зверху» з подальшим падінням гравітацією
    public void SetTransform(float spawnXRange, float spawnY)
    {
        transform.position = new Vector3(Random.Range(-spawnXRange, spawnXRange), spawnY, 0f);

        if (rb != null)
        {
            rb.useGravity = true;
            rb.drag = 0f;
            rb.angularDrag = 0.05f;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // «Космічний політ»: з-за меж екрану в напрямку цілі без гравітації
    public void LaunchFromOffscreen(Vector3 spawnPoint, Vector3 targetPoint, float speed)
    {
        transform.position = spawnPoint;
        Vector3 direction = (targetPoint - spawnPoint);
        direction.y = direction.y;          // залишаємо як є; за потреби можна фіксувати Z
        direction.Normalize();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.drag = 0f;
            rb.angularDrag = 0.05f;
            rb.velocity = direction * speed;
            rb.angularVelocity = Vector3.zero;
        }

        // орієнтуємо «вверх» уздовж напрямку (для плоских спрайтів на площині X-Y краще обертати навколо Z)
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(Vector3.forward, direction);
    }
}