using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-6000)]
public class ShellFlipbookDirector : MonoBehaviour
{
    [Header("Players (manual list)")]
    [Tooltip("Перетягни сюди 1..N FlipbookPlayer у бажаному порядку шарів")]
    public List<FlipbookPlayer> players = new List<FlipbookPlayer>();

    [Header("Random SetId ranges per player (inclusive)")]
    [Tooltip("Діапазони для випадкового вибору setId. Якщо елементів менше, ніж плеєрів — повторюється останній діапазон.")]
    public List<Vector2Int> setIdRanges = new List<Vector2Int>() { new Vector2Int(0, 0) };

    [Header("Playback (applied to all players)")]
    public int  fps = 30;
    public int  framesPerChunk = 120;
    public bool loop = false;

    [Header("Start Options")]
    [Tooltip("Автостарт після підготовки всіх плеєрів")]
    public bool autoStart = true;
    [Tooltip("Додаткова затримка перед синхронним стартом")]
    public float startDelay = 0f;

    [Header("Fade")]
    [Tooltip("Тривалість fade-in у секунди")]
    public float fadeInDuration = 0.35f;

    // ---- Runtime ----
    private int _preparedCount = 0;
    private bool _allPrepared  = false;
    private Coroutine _startRoutine;

    private List<Renderer> _renderers = new List<Renderer>();
    private List<MaterialPropertyBlock> _mpbs = new List<MaterialPropertyBlock>();

    public bool IsReady => _allPrepared;

    void Awake()
    {
        // 0) почистити null-и
        players.RemoveAll(p => p == null);

        // 1) підігнати довжину setIdRanges (якщо коротша — повторюємо останній діапазон)
        if (setIdRanges.Count == 0) setIdRanges.Add(new Vector2Int(0, 0));
        while (setIdRanges.Count < players.Count)
            setIdRanges.Add(setIdRanges[setIdRanges.Count - 1]);

        // 2) призначити випадкові setId і загальні playback-параметри
        for (int i = 0; i < players.Count; i++)
        {
            AssignRandomSetId(players[i], setIdRanges[i]);
            ApplyPlaybackDefaults(players[i]);
        }

        // 3) підготувати рендерери та виставити альфу 0 через MPB
        _renderers.Clear(); _mpbs.Clear();
        for (int i = 0; i < players.Count; i++)
            PrepareRenderer(players[i], out var r, out var mpb, 0f);
    }

    void OnEnable()
    {
        _preparedCount = 0;
        _allPrepared = false;

        // підписки + пауза до старту
        foreach (var p in players)
        {
            if (!p) continue;
            p.OnPrepareCompleted.AddListener(() => OnPrepared(p));
            p.OnError.AddListener(msg => Debug.LogError($"[{p.name}] {msg}"));
            p.Pause();
        }
    }

    void OnDisable()
    {
        foreach (var p in players)
        {
            if (!p) continue;
            p.OnPrepareCompleted.RemoveAllListeners();
            p.OnError.RemoveAllListeners();
        }
        if (_startRoutine != null)
        {
            StopCoroutine(_startRoutine);
            _startRoutine = null;
        }
    }

    // ---------- ПУБЛІЧНО ----------
    /// <summary>
    /// Ручний старт. Якщо waitUntilPrepared=true — дочекається готовності всіх, і тоді стартоне.
    /// Якщо false і не готові — нічого не робить.
    /// </summary>
    public void StartNow(bool waitUntilPrepared = true)
    {
        if (_startRoutine != null) StopCoroutine(_startRoutine);
        _startRoutine = StartCoroutine(CoStartWhenReady(waitUntilPrepared));
    }

    // ---------- ВНУТРІШНЄ ----------
    private IEnumerator CoStartWhenReady(bool wait)
    {
        if (!_allPrepared)
        {
            if (!wait) yield break;
            while (!_allPrepared) yield return null;
        }

        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        // гарантуємо початок з кадру 0 і одночасний Play()
        foreach (var p in players) { if (p) p.Stop(); }
        foreach (var p in players) { if (p) p.Play(); }

        // паралельний fade-in для всіх
        for (int i = 0; i < _renderers.Count; i++)
            StartCoroutine(CoFade(_renderers[i], _mpbs[i], 1f, fadeInDuration));

        _startRoutine = null;
    }

    private void OnPrepared(FlipbookPlayer p)
    {
        _preparedCount++;
        if (_preparedCount >= players.Count)
        {
            _allPrepared = true;
            if (autoStart)
            {
                if (_startRoutine != null) StopCoroutine(_startRoutine);
                _startRoutine = StartCoroutine(CoStartWhenReady(wait: false));
            }
        }
    }

    private void AssignRandomSetId(FlipbookPlayer p, Vector2Int range)
    {
        if (!p) return;
        int lo = Mathf.Min(range.x, range.y);
        int hi = Mathf.Max(range.x, range.y);
        p.setId = Random.Range(lo, hi + 1); // inclusive
    }

    private void ApplyPlaybackDefaults(FlipbookPlayer p)
    {
        if (!p) return;
        if (fps > 0) p.fps = fps;
        if (framesPerChunk > 0) p.framesPerChunk = framesPerChunk;
        p.loop = loop;
    }

    private void PrepareRenderer(FlipbookPlayer p, out Renderer r, out MaterialPropertyBlock mpb, float alpha)
    {
        r = null; mpb = null;
        if (!p) return;

        r = p.GetComponent<Renderer>();
        if (r != null)
        {
            mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            var col = mpb.GetVector("_Color");
            if (col == Vector4.zero) col = Color.white;
            col.w = alpha;
            mpb.SetVector("_Color", col);
            r.SetPropertyBlock(mpb);
        }

        _renderers.Add(r);
        _mpbs.Add(mpb);
    }

    private IEnumerator CoFade(Renderer r, MaterialPropertyBlock mpb, float targetAlpha, float duration)
    {
        if (!r || mpb == null) yield break;

        r.GetPropertyBlock(mpb);
        var col = (Vector4)mpb.GetVector("_Color");
        float a0 = col.w;
        float t = (duration <= 0f) ? 1f : 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            float a = Mathf.Lerp(a0, targetAlpha, Mathf.Clamp01(t));
            col.w = a;
            mpb.SetVector("_Color", col);
            r.SetPropertyBlock(mpb);
            yield return null;
        }
    }
}