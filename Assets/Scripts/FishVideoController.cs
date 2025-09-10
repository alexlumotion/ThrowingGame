using UnityEngine;
using UnityEngine.Video;

public class FishVideoController : MonoBehaviour
{
    public enum Direction { LeftToRight, RightToLeft }

    [Header("References")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Clips")]
    [Tooltip("Кліпи, де риби пливуть зліва -> направо")]
    [SerializeField] private VideoClip[] leftToRightClips;

    [Tooltip("Кліпи, де риби пливуть справа -> наліво")]
    [SerializeField] private VideoClip[] rightToLeftClips;

    [Tooltip("Кліпи третьої категорії (інтерлюдія)")]
    [SerializeField] private VideoClip[] interludeClips;

    [Header("Playback")]
    [SerializeField] private bool autoPlayOnStart = true;
    [SerializeField] private Direction startDirection = Direction.LeftToRight;
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Header("Interlude (кожні N секунд)")]
    [Tooltip("Інтервал між інтерлюдіями в секундах (за замовчуванням 5 хв = 300с)")]
    [Min(1f)] public float interludeIntervalSeconds = 300f;

    private Direction currentDirection;
    private VideoClip lastPlayedClip;

    // Інтерлюдія/таймер
    private float nextInterludeAt = Mathf.Infinity;
    private bool interludePending = false;     // позначка: треба вставити інтерлюдію після поточного кліпу
    private bool isInterludePlaying = false;   // зараз грає інтерлюдія

    void Reset()
    {
        videoPlayer = GetComponent<VideoPlayer>();
    }

    void Awake()
    {
        if (!videoPlayer) videoPlayer = GetComponent<VideoPlayer>();
        if (!videoPlayer) videoPlayer = gameObject.AddComponent<VideoPlayer>();

        videoPlayer.isLooping = false;
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.VideoClip;

        videoPlayer.loopPointReached += OnVideoEnded;
        videoPlayer.errorReceived += (vp, msg) =>
        {
            Debug.LogWarning($"[FishVideoController] Video error: {msg}");
            // якщо сталася помилка — спробуємо перейти далі за поточним станом
            HandleNextOnEnd();
        };
    }

    void Start()
    {
        currentDirection = startDirection;
        ScheduleNextInterlude(); // стартуємо таймер

        if (autoPlayOnStart)
            PlayNext(currentDirection);
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoEnded;
    }

    private void OnVideoEnded(VideoPlayer vp)
    {
        HandleNextOnEnd();
    }

    private void HandleNextOnEnd()
    {
        // Якщо щойно завершилась інтерлюдія — повертаємось до нормального циклу
        if (isInterludePlaying)
        {
            isInterludePlaying = false;
            ScheduleNextInterlude(); // переносимо таймер
            PlayNext(currentDirection); // продовжуємо з тим самим напрямком, що був до інтерлюдії
            return;
        }

        // Якщо настав час інтерлюдії або вона вже запланована — граємо інтерлюдію
        if (interludePending || Time.time >= nextInterludeAt)
        {
            interludePending = false;
            PlayInterlude();
            return;
        }

        // 🔹 Нове: міняємо напрямок після кожного звичайного кліпу
        currentDirection = currentDirection == Direction.LeftToRight
            ? Direction.RightToLeft
            : Direction.LeftToRight;

        PlayNext(currentDirection);
    }

    /// <summary>
    /// Зовнішній виклик: встановити напрямок і одразу програти наступний кліп.
    /// </summary>
    public void PlayDirection(Direction dir)
    {
        currentDirection = dir;
        // не зриваємо логіку інтерлюдії — просто граємо наступний згідно стану
        if (!isInterludePlaying)
            PlayNext(currentDirection);
    }

    /// <summary>
    /// Зовнішній виклик: перемкнути напрямок (тумблер) і програти наступний кліп (якщо не грає інтерлюдія).
    /// </summary>
    public void ToggleDirection()
    {
        currentDirection = currentDirection == Direction.LeftToRight
            ? Direction.RightToLeft
            : Direction.LeftToRight;

        if (!isInterludePlaying)
            PlayNext(currentDirection);
    }

    /// <summary>
    /// Можеш змінювати інтервал під час роботи.
    /// </summary>
    public void SetInterludeIntervalSeconds(float seconds)
    {
        interludeIntervalSeconds = Mathf.Max(1f, seconds);
        // перескеджулити наступну інтерлюдію від поточного моменту,
        // але без переривання поточного кліпу
        ScheduleNextInterlude();
    }

    /// <summary>
    /// Вручну “попросити” інтерлюдію: спрацює після завершення поточного кліпу.
    /// </summary>
    public void RequestInterlude()
    {
        interludePending = true;
    }

    private void ScheduleNextInterlude()
    {
        nextInterludeAt = Time.time + interludeIntervalSeconds;
        interludePending = false;
    }

    private void PlayInterlude()
    {
        var pool = interludeClips;
        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning("[FishVideoController] No interlude clips assigned. Continue normal playback.");
            ScheduleNextInterlude();
            PlayNext(currentDirection);
            return;
        }

        var next = PickClip(pool);
        if (next == null)
        {
            Debug.LogWarning("[FishVideoController] Failed to pick interlude clip. Continue normal playback.");
            ScheduleNextInterlude();
            PlayNext(currentDirection);
            return;
        }

        isInterludePlaying = true;
        lastPlayedClip = next;
        videoPlayer.Stop();
        videoPlayer.clip = next;
        videoPlayer.Play();
    }

    private void PlayNext(Direction dir)
    {
        var pool = GetPool(dir);
        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning($"[FishVideoController] No clips for direction {dir}");
            return;
        }

        var next = PickClip(pool);
        if (next == null)
        {
            Debug.LogWarning("[FishVideoController] Failed to pick next clip.");
            return;
        }

        lastPlayedClip = next;

        videoPlayer.Stop();
        videoPlayer.clip = next;
        videoPlayer.Play();
    }

    private VideoClip[] GetPool(Direction dir)
    {
        return dir == Direction.LeftToRight ? leftToRightClips : rightToLeftClips;
    }

    private VideoClip PickClip(VideoClip[] pool)
    {
        if (pool.Length == 1) return pool[0];

        int tries = 0;
        VideoClip choice;
        do
        {
            choice = pool[Random.Range(0, pool.Length)];
            tries++;
            if (!avoidImmediateRepeat || lastPlayedClip == null) break;
        } while (choice == lastPlayedClip && tries < 8);

        return choice;
    }

    // (опційно) якщо хочеш форсувати інтерлюдію по таймеру без очікування кінця кліпу:
    // можеш у Update перевіряти час і ставити interludePending = true
    // але ти просив чекати завершення поточного відео — тож тут не потрібно.
}