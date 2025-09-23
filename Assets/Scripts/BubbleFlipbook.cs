using UnityEngine;
using RenderHeads.Media.AVProVideo;

public class BubbleFlipbook : MonoBehaviour
{
    [Header("AVPro players")]
    [SerializeField] private MediaPlayer mpBubble;
    [SerializeField] private MediaPlayer mpWave;

    [Header("Clips (MediaReferences)")]
    [SerializeField] private MediaReference[] bubbleVideos;
    [SerializeField] private MediaReference[] waveVideos;

    [Header("Target renderer (матеріал із шейдером на 2-3 шари)")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private int materialIndex = 0;

    [Header("Shader property names")]
    [SerializeField] private string propLayer0Opacity = "_Layer0Opacity"; // Bubble
    [SerializeField] private string propLayer1Opacity = "_Layer1Opacity"; // Wave

    [Header("Fade")]
    [Min(0f)][SerializeField] private float fadeInDuration = 0.15f;

    [Header("Trigger for test")]
    public bool start = false;

    // runtime
    private bool readyBubble = false;
    private bool readyWave   = false;
    private MediaReference lastBubbleRef;
    private MediaReference lastWaveRef;

    private MaterialPropertyBlock _mpb;

    void Reset()
    {
        mpBubble = GetComponent<MediaPlayer>();
    }

    void Awake()
    {
        SetupPlayer(mpBubble);
        SetupPlayer(mpWave);

        // стартово — прозоро
        EnsureMPB();
        SetOpacities(0f, 0f);

        // підписки
        if (mpBubble) mpBubble.Events.AddListener(OnMediaEvent);
        if (mpWave)   mpWave.Events.AddListener(OnMediaEvent);
    }

    void OnDestroy()
    {
        if (mpBubble) mpBubble.Events.RemoveListener(OnMediaEvent);
        if (mpWave)   mpWave.Events.RemoveListener(OnMediaEvent);
    }

    void Update()
    {
        if (start)
        {
            start = false;
            PlayOnce();
        }
    }

    private void SetupPlayer(MediaPlayer mp)
    {
        if (!mp) return;
        mp.AutoOpen = false;
        mp.Loop     = false;
        // решта опцій за замовчуванням
    }

    public void PlayOnce()
    {
        if (!mpBubble || !mpWave || bubbleVideos == null || waveVideos == null ||
            bubbleVideos.Length == 0 || waveVideos.Length == 0 || targetRenderer == null)
        {
            Debug.LogWarning("[BubbleFlipbook] Not configured");
            return;
        }

        // скидаємо стани готовності
        readyBubble = readyWave = false;

        // ховаємо (через опакіті), щоб не було мигу
        SetOpacities(0f, 0f);

        // вибираємо випадкові кліпи (без негайного повтору)
        var bubbleRef = PickRef(bubbleVideos, lastBubbleRef);
        var waveRef   = PickRef(waveVideos,   lastWaveRef);

        lastBubbleRef = bubbleRef;
        lastWaveRef   = waveRef;

        // відкриваємо, але не граємо до FirstFrameReady
        mpBubble.OpenMedia(bubbleRef, autoPlay: false);
        mpWave.OpenMedia(waveRef,     autoPlay: false);
    }

    // === AVPro events ===
    private void OnMediaEvent(MediaPlayer mp, MediaPlayerEvent.EventType evt, ErrorCode error)
    {
        switch (evt)
        {
            case MediaPlayerEvent.EventType.FirstFrameReady:
                if (mp == mpBubble) readyBubble = true;
                if (mp == mpWave)   readyWave   = true;

                // коли обидва готові — плавно показуємо й стартуємо
                if (readyBubble && readyWave)
                    StartPlayback();
                break;

            case MediaPlayerEvent.EventType.FinishedPlaying:
                // закінчився bubble — вважаємо цикл завершеним
                if (mp == mpBubble)
                {
                    // сховати й повернути у пул
                    SetOpacities(0f, 0f);
                    BubbleFlipbookPool.Instance.ReturnToPool(this.gameObject);
                }
                break;

            case MediaPlayerEvent.EventType.Error:
                Debug.LogWarning($"[BubbleFlipbook] AVPro error: {error} on {mp?.name}");
                break;
        }
    }

    private void StartPlayback()
    {
        // гарантуємо нульову видимість перед стартом
        SetOpacities(0f, 0f);

        // запускаємо обидва плеєри
        mpWave.Play();
        mpBubble.Play();

        // піднімаємо опакіті з fade
        if (fadeInDuration <= 0.0001f)
        {
            SetOpacities(1f, 1f);
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(FadeInRoutine(fadeInDuration));
        }
    }

    // === Helpers ===

    private MediaReference PickRef(MediaReference[] pool, MediaReference last)
    {
        if (pool.Length == 1) return pool[0];
        int tries = 0;
        MediaReference choice;
        do
        {
            choice = pool[Random.Range(0, pool.Length)];
            tries++;
        } while (choice == last && tries < 8);
        return choice;
    }

    private System.Collections.IEnumerator FadeInRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);
            SetOpacities(a, a);
            yield return null;
        }
        SetOpacities(1f, 1f);
    }

    private void EnsureMPB()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    private void SetOpacities(float bubbleA, float waveA)
    {
        EnsureMPB();
        targetRenderer.GetPropertyBlock(_mpb, materialIndex);
        if (!string.IsNullOrEmpty(propLayer0Opacity)) _mpb.SetFloat(propLayer0Opacity, bubbleA);
        if (!string.IsNullOrEmpty(propLayer1Opacity)) _mpb.SetFloat(propLayer1Opacity, waveA);
        targetRenderer.SetPropertyBlock(_mpb, materialIndex);
    }
}