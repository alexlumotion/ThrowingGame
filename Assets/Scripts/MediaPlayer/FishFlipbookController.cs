using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class FishFlipbookController : MonoBehaviour
{
    public enum Direction { LeftToRight, RightToLeft }

    [Header("Required")]
    public FlipbookPlayer player;

    [Header("Sets (use setId from FlipbookAssetManager)")]
    public List<int> leftToRightSets = new List<int>();
    public List<int> rightToLeftSets = new List<int>();
    [Tooltip("Єдина інтерлюдія (setId). Якщо <0 — інтерлюдія вимкнена")]
    public int interludeSetId = -1;

    [Header("Start Options")]
    public bool autoPlayOnStart = true;
    public Direction startDirection = Direction.LeftToRight;

    [Header("Interlude")]
    [Tooltip("Інтервал між інтерлюдіями, сек. Якщо ≤0 — інтерлюдія вимкнена")]
    public float interludeIntervalSeconds = 0f;

    [Header("Repeat / Random")]
    public bool avoidImmediateRepeat = true;

    [Header("Flow")]
    [Tooltip("Перемикати напрямок після кожного звичайного кліпу (L→R→L→R...)")]
    public bool alternateDirections = true;

    [Header("Events (optional)")]
    public UnityEvent<int> OnClipStarted;    // setId
    public UnityEvent<int> OnClipFinished;   // setId
    public UnityEvent OnInterludeStarted;
    public UnityEvent OnInterludeEnded;
    public UnityEvent<string> OnError;

    // ---- runtime state ----
    private Direction _currentDirection;
    private int _lastPlayedSetId = -9999;
    private bool _interludePending = false;
    private bool _isInterludePlaying = false;
    private Coroutine _interludeTimerCo;

    void Awake()
    {
        if (!player) player = GetComponent<FlipbookPlayer>();
    }

    void OnEnable()
    {
        if (player)
        {
            player.OnFinished.AddListener(OnPlayerFinished);
            player.OnError.AddListener(err => OnError?.Invoke(err));
        }

        _currentDirection = startDirection;
        StartInterludeTimerIfNeeded();

        if (autoPlayOnStart)
            PlayDirection(_currentDirection);
    }

    void OnDisable()
    {
        if (player)
        {
            player.OnFinished.RemoveAllListeners();
            player.OnError.RemoveAllListeners();
        }
        if (_interludeTimerCo != null)
        {
            StopCoroutine(_interludeTimerCo);
            _interludeTimerCo = null;
        }
    }

    // ===== PUBLIC API =====
    public void PlayDirection(Direction dir)
    {
        _currentDirection = dir;
        if (_isInterludePlaying) return; // не перериваємо інтерлюдію

        int nextSetId = PickNextSetId(dir);
        PlaySet(nextSetId);
    }

    public void ToggleDirection()
    {
        _currentDirection = Opposite(_currentDirection);
        PlayDirection(_currentDirection);
    }

    public void PlayNextNow()
    {
        if (_isInterludePlaying) return;

        if (_interludePending && interludeSetId >= 0)
        {
            _interludePending = false;
            PlayInterlude();
        }
        else
        {
            // зіграти наступний у поточному напрямку (без примусового toggl’у)
            PlayDirection(_currentDirection);
        }
    }

    public void Pause()  { if (player) player.Pause(); }
    public void Stop()   { if (player) player.Stop(); }
    public void Resume() { if (player) player.Play(); }

    public void SetInterludeIntervalSeconds(float seconds)
    {
        interludeIntervalSeconds = seconds;
        RestartInterludeTimerIfNeeded();
    }

    // ===== INTERNAL =====
    private void OnPlayerFinished()
    {
        Debug.Log("FINISHEDDD!!!!!!!!");
        // завершився поточний ролик
        OnClipFinished?.Invoke(_lastPlayedSetId);

        // Якщо це була інтерлюдія — повертаємось до попереднього напрямку, БЕЗ перемикання
        if (_isInterludePlaying)
        {
            _isInterludePlaying = false;
            OnInterludeEnded?.Invoke();
            StartInterludeTimerIfNeeded();       // запланувати наступну інтерлюдію
            PlayDirection(Opposite(_currentDirection));    // продовжити звичайний флоу
            Debug.Log($"[FishFlipbookController] Interlude ended → back to direction={_currentDirection}");
            return;
        }

        // Зіграємо інтерлюдію, якщо заплановано
        if (_interludePending && interludeSetId >= 0)
        {
            _interludePending = false;
            Debug.Log($"[FishFlipbookController] Starting interlude setId={interludeSetId}");
            PlayInterlude();
            return;
        }

        // Звичайний режим: чергувати напрямки (якщо увімкнено)
        if (alternateDirections)
            _currentDirection = Opposite(_currentDirection);

        // оберемо наступний сет усередині PlayDirection
        Debug.Log($"[FishFlipbookController] Finished setId={_lastPlayedSetId} → next direction={_currentDirection}");
        PlayDirection(_currentDirection);
    }

    private void PlayInterlude()
    {
        if (interludeSetId < 0 || player == null) return;
        _isInterludePlaying = true;
        OnInterludeStarted?.Invoke();
        PlaySet(interludeSetId);
    }

    private int PickNextSetId(Direction dir)
    {
        var list = (dir == Direction.LeftToRight) ? leftToRightSets : rightToLeftSets;
        if (list == null || list.Count == 0) return _lastPlayedSetId;

        if (!avoidImmediateRepeat || list.Count == 1)
            return list[Random.Range(0, list.Count)];

        int pick;
        int guard = 16;
        do
        {
            pick = list[Random.Range(0, list.Count)];
        } while (pick == _lastPlayedSetId && --guard > 0);

        return pick;
    }

    private void PlaySet(int setId)
    {
        if (player == null) return;
        _lastPlayedSetId = setId;
        Debug.Log($"[FishFlipbookController] Now playing '{ResolveSetName(setId)}'");

        player.setId = setId;
        player.loop = false;   // запобігаємо зацикленню ролика
        player.Stop();         // починаємо з 0-го кадру
        player.Play();

        OnClipStarted?.Invoke(setId);
    }

    private Direction Opposite(Direction d)
        => (d == Direction.LeftToRight) ? Direction.RightToLeft : Direction.LeftToRight;

    private string ResolveSetName(int setId)
    {
        var set = player?.manager?.GetSet(setId);
        if (set == null) return $"setId={setId}";
        return string.IsNullOrEmpty(set.name) ? $"setId={setId}" : set.name;
    }

    // ===== Interlude timer =====
    private void StartInterludeTimerIfNeeded()
    {
        if (interludeIntervalSeconds > 0f && interludeSetId >= 0)
        {
            if (_interludeTimerCo != null) StopCoroutine(_interludeTimerCo);
            _interludeTimerCo = StartCoroutine(CoInterludeTimer(interludeIntervalSeconds));
        }
    }

    private void RestartInterludeTimerIfNeeded()
    {
        if (_interludeTimerCo != null)
        {
            StopCoroutine(_interludeTimerCo);
            _interludeTimerCo = null;
        }
        StartInterludeTimerIfNeeded();
    }

    private IEnumerator CoInterludeTimer(float seconds)
    {
        while (true)
        {
            yield return new WaitForSeconds(seconds);
            if (!_isInterludePlaying)
                _interludePending = true;
        }
    }
}
