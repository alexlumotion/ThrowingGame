using System.Collections;
using UnityEngine;

public class BubbleFlipbook : MonoBehaviour
{
    [Header("Animators")]
    public Animator bubbleAnimator;
    public Animator waveAnimator;

    [Header("State Name Prefixes")]
    public string bubbleStatePrefix = "bubble_";
    public string waveStatePrefix = "wave_";

    [Header("Inclusive State Ranges")]
    public Vector2Int bubbleStateRange = new Vector2Int(1, 6);
    public Vector2Int waveStateRange = new Vector2Int(1, 12);

    [Header("Sprite Renderers")]
    public SpriteRenderer bubbleRenderer;
    public SpriteRenderer waveRenderer;

    [Header("Плавний фейд увімкнення (сек)")]
    public float fadeInDuration = 0.15f;

    [Header("Тригер для тесту з інспектора")]
    public bool start = false;

    int finishedCount = 0;
    int playSessionId = 0;

    Coroutine bubbleCoroutine;
    Coroutine waveCoroutine;

    void Awake()
    {
        HideRendererImmediate(bubbleRenderer);
        HideRendererImmediate(waveRenderer);
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
        if (!bubbleAnimator || !waveAnimator)
        {
            Debug.LogWarning("BubbleFlipbook: не налаштовані Animator.");
            return;
        }

        playSessionId++;
        int currentSession = playSessionId;

        StopActiveCoroutines();
        finishedCount = 0;

        HideRendererImmediate(bubbleRenderer);
        HideRendererImmediate(waveRenderer);

        string bubbleState = GetRandomStateName(bubbleStatePrefix, bubbleStateRange);
        string waveState = GetRandomStateName(waveStatePrefix, waveStateRange);

        if (string.IsNullOrEmpty(bubbleState) || string.IsNullOrEmpty(waveState))
        {
            Debug.LogWarning("BubbleFlipbook: не вдалося підібрати назви станів для аніматорів.");
            return;
        }

        PlayState(bubbleAnimator, bubbleState);
        PlayState(waveAnimator, waveState);

        ShowRenderer(bubbleRenderer, fadeInDuration);
        ShowRenderer(waveRenderer, fadeInDuration);

        bubbleCoroutine = StartCoroutine(TrackAnimation(bubbleAnimator, bubbleState, currentSession));
        waveCoroutine = StartCoroutine(TrackAnimation(waveAnimator, waveState, currentSession));
    }

    void StopActiveCoroutines()
    {
        if (bubbleCoroutine != null) StopCoroutine(bubbleCoroutine);
        if (waveCoroutine != null) StopCoroutine(waveCoroutine);
        bubbleCoroutine = null;
        waveCoroutine = null;
    }

    void PlayState(Animator animator, string stateName)
    {
        if (!animator) return;
        animator.speed = 1f;
        animator.Play(stateName, 0, 0f);
        animator.Update(0f);
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
                yield break;
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
            Debug.LogWarning($"BubbleFlipbook: Animator {animator.name} не має runtimeAnimatorController.");
            return 0f;
        }

        foreach (var clip in controller.animationClips)
        {
            if (clip && clip.name == stateName)
            {
                return Mathf.Max(clip.length, 0f);
            }
        }

        Debug.LogWarning($"BubbleFlipbook: не знайшов AnimationClip \"{stateName}\" у контролері {animator.name}.");
        return 0f;
    }

    string GetRandomStateName(string prefix, Vector2Int range)
    {
        if (range.x > range.y)
        {
            Debug.LogWarning($"BubbleFlipbook: хибний діапазон для префікса {prefix} ({range.x}-{range.y}).");
            return null;
        }

        int randomIndex = Random.Range(range.x, range.y + 1);
        return $"{prefix}{randomIndex}";
    }

    void OnAnimationFinished(int sessionId)
    {
        if (sessionId != playSessionId) return;

        finishedCount++;
        if (finishedCount >= 2)
        {
            HideRendererImmediate(bubbleRenderer);
            HideRendererImmediate(waveRenderer);

            BubbleFlipbookPool.Instance.ReturnToPool(this.gameObject);
        }
    }

    void HideRendererImmediate(SpriteRenderer sr)
    {
        if (!sr) return;
        var c = sr.color;
        c.a = 0f;
        sr.color = c;
        sr.enabled = true;
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
