using UnityEngine;
using RenderHeads.Media.AVProVideo;
using System.Collections;

public class ShellFlipbook : MonoBehaviour
{
    [Header("Players")]
    public MediaPlayer mpWave;
    public MediaPlayer mpShell;
    public MediaPlayer mpBubble;

    [Header("Clips")]
    public MediaReference[] waveVideos;
    public MediaReference[] shellVideos;
    public MediaReference[] bubbleVideos;

    [Header("Target Renderer + Material (TripleVideo shader)")]
    public Renderer targetRenderer;   // один Renderer із матеріалом TripleVideoURP
    public int materialIndex = 0;     // якщо у рендерера кілька матеріалів

    [Header("Fade")]
    public float fadeInDuration = 0.15f;

    [Header("Trigger")]
    public bool start = false;

    // внутрішній стан
    private int readyCount = 0;
    private int finishedCount = 0;

    // кеш імена пропертей (щоб не писати строки кожен раз)
    private static readonly int Layer0OpacityID = Shader.PropertyToID("_Layer0Opacity");
    private static readonly int Layer1OpacityID = Shader.PropertyToID("_Layer1Opacity");
    private static readonly int Layer2OpacityID = Shader.PropertyToID("_Layer2Opacity");

    private Material _matInstance;

    void Awake()
    {
        // Матеріал: створимо інстанс, щоб не правити sharedMaterial
        if (targetRenderer)
        {
            var mats = targetRenderer.materials;
            if (materialIndex < 0 || materialIndex >= mats.Length)
            {
                Debug.LogError("ShellFlipbookAVPro: materialIndex поза діапазоном.");
            }
            else
            {
                // інстансуємо матеріал
                mats[materialIndex] = new Material(mats[materialIndex]);
                targetRenderer.materials = mats;
                _matInstance = mats[materialIndex];

                // стартово сховаємо всі шари
                _matInstance.SetFloat(Layer0OpacityID, 0f);
                _matInstance.SetFloat(Layer1OpacityID, 0f);
                _matInstance.SetFloat(Layer2OpacityID, 0f);
            }
        }

        SetupPlayer(mpWave);
        SetupPlayer(mpShell);
        SetupPlayer(mpBubble);
    }

    void Update()
    {
        if (start)
        {
            start = false;
            PlayOnce();
        }
    }

    void SetupPlayer(MediaPlayer mp)
    {
        if (!mp) return;
        mp.AutoOpen = false;
        mp.Loop = false;
        mp.Events.AddListener(OnMediaEvent);
    }

    public void PlayOnce()
    {
        if (!mpWave || !mpShell || !mpBubble ||
            waveVideos == null || shellVideos == null || bubbleVideos == null ||
            waveVideos.Length == 0 || shellVideos.Length == 0 || bubbleVideos.Length == 0)
        {
            Debug.LogWarning("ShellFlipbookAVPro: не налаштовані MediaPlayer або масиви кліпів.");
            return;
        }

        readyCount = 0;
        finishedCount = 0;

        // обираємо випадкові кліпи
        var waveRef   = waveVideos[Random.Range(0, waveVideos.Length)];
        var shellRef  = shellVideos[Random.Range(0, shellVideos.Length)];
        var bubbleRef = bubbleVideos[Random.Range(0, bubbleVideos.Length)];

        // відкриваємо БЕЗ автоплея — чекаємо FirstFrameReady усіх трьох
        mpWave.OpenMedia(waveRef,  autoPlay: false);
        mpShell.OpenMedia(shellRef, autoPlay: false);
        mpBubble.OpenMedia(bubbleRef,autoPlay: false);

        // шари — прозорі, щоб не було блимання
        if (_matInstance)
        {
            _matInstance.SetFloat(Layer0OpacityID, 0f);
            _matInstance.SetFloat(Layer1OpacityID, 0f);
            _matInstance.SetFloat(Layer2OpacityID, 0f);
        }
    }

    // AVPro події
    private void OnMediaEvent(MediaPlayer mp, MediaPlayerEvent.EventType evt, ErrorCode error)
    {
        switch (evt)
        {
            case MediaPlayerEvent.EventType.FirstFrameReady:
                OnPrepared(mp);
                break;

            case MediaPlayerEvent.EventType.FinishedPlaying:
                OnOneFinished(mp);
                break;

            case MediaPlayerEvent.EventType.Error:
                Debug.LogWarning($"ShellFlipbookAVPro: AVPro error {error} on {mp?.name}");
                // вважатимемо завершеним, щоб не зависнути
                OnOneFinished(mp);
                break;
        }
    }

    // коли кожен із трьох підготував перший кадр
    void OnPrepared(MediaPlayer mp)
    {
        readyCount++;
        if (readyCount >= 3)
        {
            // одночасний старт
            mpWave.Play();
            mpShell.Play();
            mpBubble.Play();

            // плавно підняти опакіті шарів
            if (gameObject.activeInHierarchy)
                StartCoroutine(FadeLayersIn());
        }
    }

    IEnumerator FadeLayersIn()
    {
        float t = 0f;
        float dur = Mathf.Max(0.0001f, fadeInDuration);

        // стартові значення
        float a0 = _matInstance ? _matInstance.GetFloat(Layer0OpacityID) : 0f;
        float a1 = _matInstance ? _matInstance.GetFloat(Layer1OpacityID) : 0f;
        float a2 = _matInstance ? _matInstance.GetFloat(Layer2OpacityID) : 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);

            if (_matInstance)
            {
                _matInstance.SetFloat(Layer0OpacityID, Mathf.Lerp(a0, 1f, k));
                _matInstance.SetFloat(Layer1OpacityID, Mathf.Lerp(a1, 1f, k));
                _matInstance.SetFloat(Layer2OpacityID, Mathf.Lerp(a2, 1f, k));
            }
            yield return null;
        }

        if (_matInstance)
        {
            _matInstance.SetFloat(Layer0OpacityID, 1f);
            _matInstance.SetFloat(Layer1OpacityID, 1f);
            _matInstance.SetFloat(Layer2OpacityID, 1f);
        }
    }

    // Рахуємо, коли усі три дограли
    void OnOneFinished(MediaPlayer mp)
    {
        finishedCount++;
        if (finishedCount >= 3)
        {
            // скинемо опакіті, щоб не мигало при наступному старті
            if (_matInstance)
            {
                _matInstance.SetFloat(Layer0OpacityID, 0f);
                _matInstance.SetFloat(Layer1OpacityID, 0f);
                _matInstance.SetFloat(Layer2OpacityID, 0f);
            }

            // повертаємо у пул (як у тебе було)
            var pool = ShellFlipbookPool.Instance;
            if (pool != null) pool.ReturnToPool(this.gameObject);
            else gameObject.SetActive(false);
        }
    }
}