using System.Collections;
using UnityEngine;

public class ShellFlipbook : MonoBehaviour
{
    [Header("Animators")]
    public Animator waveAnimator;
    public Animator shellAnimator;
    public Animator bubbleAnimator;

    [Header("State Name Prefixes")]
    public string waveStatePrefix = "wave_";
    public string shellStatePrefix = "shell_";
    public string bubbleStatePrefix = "bubble_";

    [Header("Inclusive State Ranges")]
    public Vector2Int waveStateRange = new Vector2Int(1, 12);
    public Vector2Int shellStateRange = new Vector2Int(1, 11);
    public Vector2Int bubbleStateRange = new Vector2Int(1, 6);

    [Header("Sprite Renderers (кожен плеєр рендерить у свій матеріал/RT)")]
    public SpriteRenderer waveRenderer;
    public SpriteRenderer shellRenderer;
    public SpriteRenderer bubbleRenderer;

    [Header("Плавний фейд увімкнення (сек)")]
    public float fadeInDuration = 0.15f;

    [Header("Тригер для тесту з інспектора")]
    public bool start = false;

    // внутрішній стан
    int finishedCount = 0;
    int playSessionId = 0;

    Coroutine waveCoroutine;
    Coroutine shellCoroutine;
    Coroutine bubbleCoroutine;

    void Awake()
    {
        // стартово сховати всі візуали
        HideRendererImmediate(waveRenderer);
        HideRendererImmediate(shellRenderer);
        HideRendererImmediate(bubbleRenderer);
    }

    void Update()
    {
        if (start)
        {
            start = false;
            PlayOnce();
        }
    }

    public void PlayOnce()
    {
        if (!waveAnimator || !shellAnimator || !bubbleAnimator)
        {
            Debug.LogWarning("ShellFlipbook: не налаштовані Animator.");
            return;
        }

        // інкрементуємо сесію, щоб старі корутини ігнорувалися
        playSessionId++;
        int currentSession = playSessionId;

        StopActiveCoroutines();

        finishedCount = 0;

        // сховати візуал, щоб не було мигу між кліпами
        HideRendererImmediate(waveRenderer);
        HideRendererImmediate(shellRenderer);
        HideRendererImmediate(bubbleRenderer);

        string waveState = GetRandomStateName(waveStatePrefix, waveStateRange);
        string shellState = GetRandomStateName(shellStatePrefix, shellStateRange);
        string bubbleState = GetRandomStateName(bubbleStatePrefix, bubbleStateRange);

        if (string.IsNullOrEmpty(waveState) ||
            string.IsNullOrEmpty(shellState) ||
            string.IsNullOrEmpty(bubbleState))
        {
            Debug.LogWarning("ShellFlipbook: не вдалося підібрати назви станів для аніматорів.");
            return;
        }

        // миттєво намалювати перший кадр кожної анімації
        PlayState(waveAnimator, waveState);
        PlayState(shellAnimator, shellState);
        PlayState(bubbleAnimator, bubbleState);

        // Плавно показати всі три (без мигу) і запустити трекінг
        ShowRenderer(waveRenderer, fadeInDuration);
        ShowRenderer(shellRenderer, fadeInDuration);
        ShowRenderer(bubbleRenderer, fadeInDuration);

        waveCoroutine = StartCoroutine(TrackAnimation(waveAnimator, waveState, currentSession));
        shellCoroutine = StartCoroutine(TrackAnimation(shellAnimator, shellState, currentSession));
        bubbleCoroutine = StartCoroutine(TrackAnimation(bubbleAnimator, bubbleState, currentSession));
    }

    void StopActiveCoroutines()
    {
        if (waveCoroutine != null) StopCoroutine(waveCoroutine);
        if (shellCoroutine != null) StopCoroutine(shellCoroutine);
        if (bubbleCoroutine != null) StopCoroutine(bubbleCoroutine);

        waveCoroutine = null;
        shellCoroutine = null;
        bubbleCoroutine = null;
    }

    void PlayState(Animator animator, string stateName)
    {
        if (!animator) return;
        animator.speed = 1f;
        animator.Play(stateName, 0, 0f);
        animator.Update(0f); // змусити застосувати перший кадр
    }

    IEnumerator TrackAnimation(Animator animator, string stateName, int sessionId)
    {
        if (!animator)
        {
            OnAnimationFinished(sessionId);
            yield break;
        }

        float duration = GetClipLength(animator, stateName);
        if (duration <= 0f)
        {
            OnAnimationFinished(sessionId);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (sessionId != playSessionId)
            {
                yield break; // нова сесія вже стартувала
            }

            elapsed += Time.deltaTime * animator.speed;
            yield return null;
        }

        OnAnimationFinished(sessionId);
    }

    float GetClipLength(Animator animator, string stateName)
    {
        if (!animator) return 0f;

        var controller = animator.runtimeAnimatorController;
        if (!controller)
        {
            Debug.LogWarning($"ShellFlipbook: Animator {animator.name} не має runtimeAnimatorController.");
            return 0f;
        }

        foreach (var clip in controller.animationClips)
        {
            if (clip && clip.name == stateName)
            {
                return Mathf.Max(clip.length, 0f);
            }
        }

        Debug.LogWarning($"ShellFlipbook: не знайшов AnimationClip \"{stateName}\" у контролері {animator.name}.");
        return 0f;
    }

    string GetRandomStateName(string prefix, Vector2Int range)
    {
        if (range.x > range.y)
        {
            Debug.LogWarning($"ShellFlipbook: хибний діапазон для префікса {prefix} ({range.x}-{range.y}).");
            return null;
        }

        int randomIndex = Random.Range(range.x, range.y + 1);
        return $"{prefix}{randomIndex}";
    }

    // Рахуємо завершення кожного аніматора; коли всі 3 — повертаємо у пул
    void OnAnimationFinished(int sessionId)
    {
        if (sessionId != playSessionId) return;

        finishedCount++;
        if (finishedCount >= 3)
        {
            HideRendererImmediate(waveRenderer);
            HideRendererImmediate(shellRenderer);
            HideRendererImmediate(bubbleRenderer);

            ShellFlipbookPool.Instance.ReturnToPool(this.gameObject);
        }
    }

    // --- утиліти для видимості SpriteRenderer ---

    void HideRendererImmediate(SpriteRenderer sr)
    {
        if (!sr) return;
        var c = sr.color;
        c.a = 0f;
        sr.color = c;
        sr.enabled = true; // залишаємо ввімкненим, але прозорим (щоб не мигав при вмиканні)
    }

    void ShowRenderer(SpriteRenderer sr, float duration)
    {
        if (!sr) return;
        sr.enabled = true;
        StartCoroutine(FadeSprite(sr, 1f, Mathf.Max(0.0001f, duration)));
    }

    IEnumerator FadeSprite(SpriteRenderer sr, float targetAlpha, float duration)
    {
        float t = 0f;
        var c = sr.color;
        float startA = c.a;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(startA, targetAlpha, t / duration);
            sr.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }
        sr.color = new Color(c.r, c.g, c.b, targetAlpha);
    }
}
