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

    [Header("Playback")]
    [SerializeField] private bool autoPlayOnStart = true;
    [SerializeField] private Direction startDirection = Direction.LeftToRight;

    [Tooltip("Заборонити повтор того ж кліпу двічі поспіль")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    private Direction currentDirection;
    private VideoClip lastPlayedClip;

    void Reset()
    {
        videoPlayer = GetComponent<VideoPlayer>();
    }

    void Awake()
    {
        //if (!videoPlayer) videoPlayer = GetComponent<VideoPlayer>();
        //if (!videoPlayer) videoPlayer = gameObject.AddComponent<VideoPlayer>();

        videoPlayer.isLooping = false;
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.VideoClip;

        videoPlayer.loopPointReached += OnVideoEnded;
        // На випадок помилок відтворення — спробуємо перейти далі
        videoPlayer.errorReceived += (vp, msg) =>
        {
            Debug.LogWarning($"[FishVideoController] Video error: {msg}");
            PlayNext(currentDirection);
        };
    }

    void Start()
    {
        currentDirection = startDirection;
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
        // коли кліп закінчився — запускаємо наступний у поточному напрямку
        //PlayNext(currentDirection);
        ToggleDirection();
    }

    /// <summary>
    /// Зовнішній виклик: встановити напрямок і одразу програти наступний кліп.
    /// Напрямок: LeftToRight або RightToLeft.
    /// </summary>
    public void PlayDirection(Direction dir)
    {
        currentDirection = dir;
        PlayNext(currentDirection);
    }

    /// <summary>
    /// Зовнішній виклик: перемкнути напрямок (тумблер) і програти наступний кліп.
    /// </summary>
    public void ToggleDirection()
    {
        currentDirection = currentDirection == Direction.LeftToRight
            ? Direction.RightToLeft
            : Direction.LeftToRight;
        PlayNext(currentDirection);
    }

    /// <summary>
    /// Запустити випадковий кліп у зазначеному напрямку.
    /// </summary>
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

        // випадковий кліп, без миттєвого повтору (якщо увімкнено)
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

    // Додатково: зовнішній доступ до поточного напрямку
    public Direction GetCurrentDirection() => currentDirection;
}