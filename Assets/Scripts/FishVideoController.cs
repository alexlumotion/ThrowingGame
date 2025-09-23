using UnityEngine;
using RenderHeads.Media.AVProVideo;

public class FishVideoController : MonoBehaviour
{
    public enum Direction { LeftToRight, RightToLeft }

    [Header("AVPro")]
    [SerializeField] private MediaPlayer mediaPlayer;

    [Header("Output (вимикаємо на час підготовки кадру)")]

    [SerializeField] private Renderer videoRenderer; 

    [Header("Clips (MediaReferences)")]
    [SerializeField] private MediaReference[] leftToRight;
    [SerializeField] private MediaReference[] rightToLeft;
    [SerializeField] private MediaReference[] interludes;

    [Header("Playback")]
    [SerializeField] private bool autoPlayOnStart = true;
    [SerializeField] private Direction startDirection = Direction.LeftToRight;
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Header("Interlude (кожні N секунд)")]
    [Min(1f)] public float interludeIntervalSeconds = 300f;

    private Direction currentDirection;
    private MediaReference lastPlayedRef;

    private float nextInterludeAt = Mathf.Infinity;
    private bool interludePending = false;
    private bool isInterludePlaying = false;

    // Що саме зараз відкриваємо (чекаємо FirstFrameReady)
    private MediaReference pendingRef = null;

    void Reset()
    {
        mediaPlayer = GetComponent<MediaPlayer>();
    }

    void Awake()
    {
        if (!mediaPlayer) mediaPlayer = GetComponent<MediaPlayer>();
        if (!mediaPlayer) mediaPlayer = gameObject.AddComponent<MediaPlayer>();

        mediaPlayer.AutoOpen = false;
        mediaPlayer.Loop = false;

        mediaPlayer.Events.AddListener(OnMediaEvent);
    }

    void Start()
    {
        currentDirection = startDirection;
        ScheduleNextInterlude();

        if (autoPlayOnStart)
        {
            // одразу відкриваємо перший, чекаємо FirstFrameReady
            StartOpenNext(PickRef(GetPool(currentDirection)));
        }
    }

    void OnDestroy()
    {
        if (mediaPlayer != null)
            mediaPlayer.Events.RemoveListener(OnMediaEvent);
    }

    // ===== AVPro events =====
    private void OnMediaEvent(MediaPlayer mp, MediaPlayerEvent.EventType evt, ErrorCode error)
    {
        switch (evt)
        {
            case MediaPlayerEvent.EventType.FinishedPlaying:
                HandleNextOnEnd();
                break;

            case MediaPlayerEvent.EventType.FirstFrameReady:
                if (pendingRef != null)
                {
                    if (videoRenderer) videoRenderer.enabled = true;
                    mediaPlayer.Play();
                    lastPlayedRef = pendingRef;
                    pendingRef = null;
                }
                break;

            case MediaPlayerEvent.EventType.Error:
                Debug.LogWarning($"[FishVideoController] AVPro error: {error}");
                HandleNextOnEnd();
                break;
        }
    }

    // ===== Переходи =====
    private void HandleNextOnEnd()
    {
        if (isInterludePlaying)
        {
            isInterludePlaying = false;
            ScheduleNextInterlude();
            StartOpenNext(PickRef(GetPool(currentDirection)));
            return;
        }

        if (interludePending || Time.time >= nextInterludeAt)
        {
            interludePending = false;
            isInterludePlaying = true;
            StartOpenNext(PickRef(interludes));
            return;
        }

        // міняємо напрямок щоразу після звичайного кліпу
        currentDirection = currentDirection == Direction.LeftToRight
            ? Direction.RightToLeft
            : Direction.LeftToRight;

        StartOpenNext(PickRef(GetPool(currentDirection)));
    }

    public void PlayDirection(Direction dir)
    {
        currentDirection = dir;
        if (!isInterludePlaying)
            StartOpenNext(PickRef(GetPool(currentDirection)));
    }

    public void ToggleDirection()
    {
        currentDirection = currentDirection == Direction.LeftToRight
            ? Direction.RightToLeft
            : Direction.LeftToRight;

        if (!isInterludePlaying)
            StartOpenNext(PickRef(GetPool(currentDirection)));
    }

    public void SetInterludeIntervalSeconds(float seconds)
    {
        interludeIntervalSeconds = Mathf.Max(1f, seconds);
        ScheduleNextInterlude();
    }

    public void RequestInterlude() => interludePending = true;

    private void ScheduleNextInterlude()
    {
        nextInterludeAt = Time.time + interludeIntervalSeconds;
        interludePending = false;
    }

    // ===== Відкриття з очікуванням FirstFrameReady =====
    private void StartOpenNext(MediaReference next)
    {
        if (next == null)
        {
            Debug.LogWarning("[FishVideoController] Next clip is null");
            return;
        }

        // Ховаємо дисплей/квад/RawImage на час підготовки кадру
        if (videoRenderer) videoRenderer.enabled = false; 

        pendingRef = next;

        // НЕ Stop(); відкриваємо з autoPlay:false і чекаємо FirstFrameReady
        mediaPlayer.OpenMedia(next, autoPlay: false);
    }

    // ===== Допоміжні =====
    private MediaReference[] GetPool(Direction dir)
    {
        return dir == Direction.LeftToRight ? leftToRight : rightToLeft;
    }

    private MediaReference PickRef(MediaReference[] pool)
    {
        if (pool == null || pool.Length == 0) return null;
        if (pool.Length == 1) return pool[0];

        int tries = 0;
        MediaReference choice;
        do
        {
            choice = pool[Random.Range(0, pool.Length)];
            tries++;
            if (!avoidImmediateRepeat || lastPlayedRef == null) break;
        } while (choice == lastPlayedRef && tries < 8);

        return choice;
    }
}