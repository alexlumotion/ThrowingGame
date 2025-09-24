using UnityEngine;
using RenderHeads.Media.AVProVideo;
using System.Collections;

public class ShellFlipbook : MonoBehaviour
{
    [Header("AVPro players")]
    [SerializeField] private MediaPlayer mpWave;
    [SerializeField] private MediaPlayer mpShell;
    [SerializeField] private MediaPlayer mpBubble;

    [Header("Clips (MediaReferences)")]
    [SerializeField] private MediaReference[] waveVideos;
    [SerializeField] private MediaReference[] shellVideos;
    [SerializeField] private MediaReference[] bubbleVideos;

    [Header("Target renderer (матеріал із шейдером на 3 шари)")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private int materialIndex = 0;

    [Header("Shader property names (opacity per layer)")]
    [SerializeField] private string propLayer0Opacity = "_Layer0Opacity"; // Wave (нижній шар)
    [SerializeField] private string propLayer1Opacity = "_Layer1Opacity"; // Shell (середній)
    [SerializeField] private string propLayer2Opacity = "_Layer2Opacity"; // Bubble (верхній)

    [Header("Fade")]
    [Min(0f)][SerializeField] private float fadeInDuration = 0.15f;

    [Header("Trigger for test")]
    public bool start = false;

    // Runtime state
    private bool readyWave   = false;
    private bool readyShell  = false;
    private bool readyBubble = false;

    private int finishedCount = 0;

    private MediaReference lastWaveRef;
    private MediaReference lastShellRef;
    private MediaReference lastBubbleRef;

    private MaterialPropertyBlock _mpb;

    void Awake()
    {
        SetupPlayer(mpWave);
        SetupPlayer(mpShell);
        SetupPlayer(mpBubble);

        // стартово — прозоро
        EnsureMPB();
        SetOpacities(0f, 0f, 0f);
    }

    void OnEnable()
    {
        if (mpWave)   mpWave.Events.AddListener(OnMediaEvent);
        if (mpShell)  mpShell.Events.AddListener(OnMediaEvent);
        if (mpBubble) mpBubble.Events.AddListener(OnMediaEvent);
    }

    void OnDisable()
    {
        if (mpWave)   mpWave.Events.RemoveListener(OnMediaEvent);
        if (mpShell)  mpShell.Events.RemoveListener(OnMediaEvent);
        if (mpBubble) mpBubble.Events.RemoveListener(OnMediaEvent);
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
        // інші опції — за потреби
    }

    public void PlayOnce()
    {
        if (!mpWave || !mpShell || !mpBubble ||
            waveVideos == null || shellVideos == null || bubbleVideos == null ||
            waveVideos.Length == 0 || shellVideos.Length == 0 || bubbleVideos.Length == 0 ||
            targetRenderer == null)
        {
            Debug.LogWarning("[ShellFlipbook] Not configured");
            return;
        }

        // скидаємо стани
        readyWave = readyShell = readyBubble = false;
        finishedCount = 0;

        // ховаємо (через опакіті), щоб не було мигу
        SetOpacities(0f, 0f, 0f);

        // вибір випадкових кліпів (із захистом від миттєвого повтору)
        var waveRef   = PickRef(waveVideos,   lastWaveRef);
        var shellRef  = PickRef(shellVideos,  lastShellRef);
        var bubbleRef = PickRef(bubbleVideos, lastBubbleRef);

        lastWaveRef   = waveRef;
        lastShellRef  = shellRef;
        lastBubbleRef = bubbleRef;

        // відкриваємо, але чекаємо FirstFrameReady усіх трьох
        mpWave.OpenMedia(waveRef,   autoPlay: false);
        mpShell.OpenMedia(shellRef, autoPlay: false);
        mpBubble.OpenMedia(bubbleRef,autoPlay: false);
    }

    // === AVPro events ===
    private void OnMediaEvent(MediaPlayer mp, MediaPlayerEvent.EventType evt, ErrorCode error)
    {
        switch (evt)
        {
            case MediaPlayerEvent.EventType.FirstFrameReady:
                if (mp == mpWave)   readyWave   = true;
                if (mp == mpShell)  readyShell  = true;
                if (mp == mpBubble) readyBubble = true;

                if (readyWave && readyShell && readyBubble)
                    StartPlayback();
                break;

            case MediaPlayerEvent.EventType.FinishedPlaying:
                // рахуємо завершення — коли всі 3 дограли, повертаємось у пул
                finishedCount++;
                if (finishedCount >= 3)
                {
                    SetOpacities(0f, 0f, 0f);
                    var pool = ShellFlipbookPool.Instance;
                    if (pool != null) pool.ReturnToPool(this.gameObject);
                    else gameObject.SetActive(false);
                }
                break;

            case MediaPlayerEvent.EventType.Error:
                Debug.LogWarning($"[ShellFlipbook] AVPro error: {error} on {mp?.name}");
                break;
        }
    }

    private void StartPlayback()
    {
        // гарантуємо нульову видимість перед стартом
        SetOpacities(0f, 0f, 0f);

        // одночасний старт трьох плеєрів
        mpWave.Play();
        mpShell.Play();
        mpBubble.Play();

        // підняти опакіті з fade
        if (fadeInDuration <= 0.0001f)
        {
            SetOpacities(1f, 1f, 1f);
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

    private IEnumerator FadeInRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);
            SetOpacities(a, a, a);
            yield return null;
        }
        SetOpacities(1f, 1f, 1f);
    }

    private void EnsureMPB()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    private void SetOpacities(float waveA, float shellA, float bubbleA)
    {
        EnsureMPB();
        targetRenderer.GetPropertyBlock(_mpb, materialIndex);

        if (!string.IsNullOrEmpty(propLayer0Opacity)) _mpb.SetFloat(propLayer0Opacity, waveA);
        if (!string.IsNullOrEmpty(propLayer1Opacity)) _mpb.SetFloat(propLayer1Opacity, shellA);
        if (!string.IsNullOrEmpty(propLayer2Opacity)) _mpb.SetFloat(propLayer2Opacity, bubbleA);

        targetRenderer.SetPropertyBlock(_mpb, materialIndex);
    }
}