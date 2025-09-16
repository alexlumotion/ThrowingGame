using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class BubbleFlipbook : MonoBehaviour
{
    [Header("Players")]
    public VideoPlayer vpBubble;
    public VideoPlayer vpWave;

    [Header("Clips")]
    public VideoClip[] bubbleVideos;
    public VideoClip[] waveVideos;

    [Header("Visual targets (один із варіантів)")]
    // Якщо рендериш на Quad/Plane — вкажи Renderer (MeshRenderer / SpriteRenderer).
    public Renderer bubbleRenderer;
    public Renderer waveRenderer;

    // Якщо рендериш у UI (RawImage) — вкажи RawImage + CanvasGroup (для fade/ввімкнення).
    public RawImage bubbleImage;
    public RawImage waveImage;
    public CanvasGroup bubbleCanvas;
    public CanvasGroup waveCanvas;

    [Header("Плавне увімкнення (мс)")]
    public float fadeInDuration = 0.15f; // 150 ms; 0 = миттєво

    [Header("Тригер для тесту з інспектора")]
    public bool start = false;

    void Awake()
    {
        // Базові налаштування плеєрів
        SetupPlayer(vpBubble);
        SetupPlayer(vpWave);

        // Стартово ховаємо візуал
        HideVisual(bubbleRenderer, bubbleImage, bubbleCanvas, true);
        HideVisual(waveRenderer,   waveImage,   waveCanvas,   true);
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
        vp.isLooping   = false;
        vp.waitForFirstFrame = true; // не показує кадри, поки перший не готовий
        // важливо: не змінюємо targetTexture/матеріали між повторами,
        // щоб уникати "білих" заповнювачів
    }

    public void PlayOnce()
    {
        if (!vpBubble || !vpWave || bubbleVideos?.Length == 0 || waveVideos?.Length == 0)
        {
            Debug.LogWarning("BubbleFlipbook: не задані VideoPlayer або масиви кліпів.");
            return;
        }

        // Вибір випадкових кліпів (по довжині масивів, без magic numbers)
        int randomBubble = Random.Range(0, bubbleVideos.Length);
        int randomWave   = Random.Range(0, waveVideos.Length);

        // Гарантовано сховати візуали, щоб між кліпами не було мигу
        HideVisual(bubbleRenderer, bubbleImage, bubbleCanvas, true);
        HideVisual(waveRenderer,   waveImage,   waveCanvas,   true);

        // Скидаємо події минулого запуску
        vpBubble.prepareCompleted -= OnPreparedBubble;
        vpWave.prepareCompleted   -= OnPreparedWave;
        vpBubble.loopPointReached -= OnBubbleEnded;

        // Призначаємо нові кліпи і готуємо
        vpBubble.Stop();
        vpWave.Stop();

        vpBubble.clip = bubbleVideos[randomBubble];
        vpWave.clip   = waveVideos[randomWave];

        vpBubble.prepareCompleted += OnPreparedBubble;
        vpWave.prepareCompleted   += OnPreparedWave;

        vpBubble.Prepare();
        vpWave.Prepare();

        // Відслідковуємо завершення саме "бульбашкового" ролика
        vpBubble.loopPointReached += OnBubbleEnded;
    }

    // --- Події підготовки ---

    void OnPreparedBubble(VideoPlayer source)
    {
        source.prepareCompleted -= OnPreparedBubble;

        // Гарантовано перемотати на старт і примусово видати перший кадр без мигу
        source.frame = 0;
        source.Play();
        source.Pause(); // тик-пауза змушує відрендерити кадр у RenderTexture/матеріал
        // тепер можна показувати візуал і стартувати
        ShowVisual(bubbleRenderer, bubbleImage, bubbleCanvas, fadeInDuration);
        source.Play();
    }

    void OnPreparedWave(VideoPlayer source)
    {
        source.prepareCompleted -= OnPreparedWave;
        source.frame = 0;
        source.Play();
        source.Pause();
        ShowVisual(waveRenderer, waveImage, waveCanvas, fadeInDuration);
        source.Play();
    }

    // --- Кінець ролика (керуємо життєвим циклом об’єкта з пулу) ---

    void OnBubbleEnded(VideoPlayer vp)
    {
        vpBubble.loopPointReached -= OnBubbleEnded;

        // Повертаємо об’єкт у пул
        BubbleFlipbookPool.Instance.ReturnToPool(this.gameObject);

        // НЕ обнуляємо матеріали/RenderTexture — це і дає "білий спалах".
        // Лише відв'яжемо кліпи (за бажанням).
        vpBubble.clip = null;
        vpWave.clip   = null;

        // На випадок, якщо об’єкт не знищується, — сховати візуали
        HideVisual(bubbleRenderer, bubbleImage, bubbleCanvas, true);
        HideVisual(waveRenderer,   waveImage,   waveCanvas,   true);
    }

    // --- Утиліти для керування видимістю ---

    void HideVisual(Renderer r, RawImage img, CanvasGroup cg, bool instantly)
    {
        if (cg)
        {
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
        else
        {
            if (img) img.enabled = false;
            if (r)   r.enabled   = false;
        }
    }

    void ShowVisual(Renderer r, RawImage img, CanvasGroup cg, float fade)
    {
        if (cg)
        {
            // простий Lerp без сторонніх бібліотек
            StartCoroutine(FadeCanvas(cg, fade <= 0f ? 0.0001f : fade));
        }
        else
        {
            if (img) img.enabled = true;
            if (r)   r.enabled   = true;
        }
    }

    System.Collections.IEnumerator FadeCanvas(CanvasGroup cg, float duration)
    {
        cg.interactable = true;
        cg.blocksRaycasts = true;

        if (duration <= 0.001f) { cg.alpha = 1f; yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }
}