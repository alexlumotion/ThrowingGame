using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class ShellFlipbook : MonoBehaviour
{
    [Header("Players")]
    public VideoPlayer vpWave;
    public VideoPlayer vpShell;
    public VideoPlayer vpBubble;

    [Header("Clips")]
    public VideoClip[] waveVideos;
    public VideoClip[] shellVideos;
    public VideoClip[] bubbleVideos;

    [Header("Sprite Renderers (кожен плеєр рендерить у свій матеріал/RT)")]
    public SpriteRenderer waveRenderer;
    public SpriteRenderer shellRenderer;
    public SpriteRenderer bubbleRenderer;

    [Header("Плавний фейд увімкнення (сек)")]
    public float fadeInDuration = 0.15f;

    [Header("Тригер для тесту з інспектора")]
    public bool start = false;

    // внутрішній стан
    int preparedCount = 0;
    int finishedCount = 0;

    void Awake()
    {
        SetupPlayer(vpWave);
        SetupPlayer(vpShell);
        SetupPlayer(vpBubble);

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

    void SetupPlayer(VideoPlayer vp)
    {
        if (!vp) return;
        vp.playOnAwake = false;
        vp.isLooping = false;
        vp.waitForFirstFrame = true; // не показувати, поки перший кадр не готовий
    }

    public void PlayOnce()
    {
        if (!vpWave || !vpShell || !vpBubble ||
            waveVideos == null || shellVideos == null || bubbleVideos == null ||
            waveVideos.Length == 0 || shellVideos.Length == 0 || bubbleVideos.Length == 0)
        {
            Debug.LogWarning("ShellFlipbook: не налаштовані VideoPlayer або масиви кліпів.");
            return;
        }

        // скинути попередні підписки/стан
        vpWave.prepareCompleted  -= OnPrepared;
        vpShell.prepareCompleted -= OnPrepared;
        vpBubble.prepareCompleted-= OnPrepared;

        vpWave.loopPointReached  -= OnOneFinished;
        vpShell.loopPointReached -= OnOneFinished;
        vpBubble.loopPointReached-= OnOneFinished;

        preparedCount = 0;
        finishedCount = 0;

        // сховати візуал, щоб не було мигу між кліпами
        HideRendererImmediate(waveRenderer);
        HideRendererImmediate(shellRenderer);
        HideRendererImmediate(bubbleRenderer);

        // зупинити, призначити випадкові кліпи, підписатись і підготувати
        vpWave.Stop();
        vpShell.Stop();
        vpBubble.Stop();

        vpWave.clip  = waveVideos[Random.Range(0, waveVideos.Length)];
        vpShell.clip = shellVideos[Random.Range(0, shellVideos.Length)];
        vpBubble.clip= bubbleVideos[Random.Range(0, bubbleVideos.Length)];

        vpWave.prepareCompleted  += OnPrepared;
        vpShell.prepareCompleted += OnPrepared;
        vpBubble.prepareCompleted+= OnPrepared;

        vpWave.Prepare();
        vpShell.Prepare();
        vpBubble.Prepare();

        // фініш-трекінг — завершення всіх трьох
        vpWave.loopPointReached  += OnOneFinished;
        vpShell.loopPointReached += OnOneFinished;
        vpBubble.loopPointReached+= OnOneFinished;
    }

    // Викликається для кожного плеєра, коли він підготовлений
    void OnPrepared(VideoPlayer vp)
    {
        preparedCount++;

        // Коли готові всі три — одночасний старт
        if (preparedCount >= 3)
        {
            // Примусово відрендерити перший кадр на всіх
            PrimeFirstFrame(vpWave);
            PrimeFirstFrame(vpShell);
            PrimeFirstFrame(vpBubble);

            // Плавно показати всі три (без мигу) і запустити
            ShowRenderer(waveRenderer, fadeInDuration);
            ShowRenderer(shellRenderer, fadeInDuration);
            ShowRenderer(bubbleRenderer, fadeInDuration);

            vpWave.Play();
            vpShell.Play();
            vpBubble.Play();
        }
    }

    // допоміжна — примусово намалювати перший кадр у RenderTexture/матеріал
    void PrimeFirstFrame(VideoPlayer vp)
    {
        if (!vp) return;
        vp.frame = 0;
        vp.Play();
        vp.Pause();
    }

    // Рахуємо завершення кожного плеєра; коли всі 3 — повертаємо у пул
    void OnOneFinished(VideoPlayer vp)
    {
        finishedCount++;
        if (finishedCount >= 3)
        {
            // опційно сховати перед поверненням у пул
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