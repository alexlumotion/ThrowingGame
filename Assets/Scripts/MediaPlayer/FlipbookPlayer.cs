using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// ==============================================================
// FlipbookPlayer with events (no Seek, no dynamic setId change)
// ==============================================================
[RequireComponent(typeof(Renderer))]
public class FlipbookPlayer : MonoBehaviour
{
    [Header("Manager & Set")]
    public FlipbookAssetManager manager;
    [Tooltip("Який набір використовувати (індекс у менеджері)")] public int setId = 0;

    [Header("Playback")]
    public int fps = 30;
    [Tooltip("Кадрів у одному чанку (має збігатися з білдом)")] public int framesPerChunk = 120;
    public bool loop = true;

    [Header("Preload")]
    [Tooltip("Почати прелоад наступного чанка, коли до кінця лишилось стільки кадрів")]
    public int preloadThresholdFrames = 30; // ~1 сек при 30 fps

    [Header("Shader Props")]
    [Tooltip("Імʼя текстурного параметра в матеріалі (Texture2DArray)")] public string textureArrayProperty = "_FlipbookTex";
    [Tooltip("Імʼя параметра-індексу кадру у шейдері")] public string frameIndexProperty = "_Frame";

    [Header("Placeholder (optional)")]
    [Tooltip("Плейсхолдер Texture2DArray, що показується до завантаження першого чанка")]
    public Texture2DArray placeholderArray;
    [Tooltip("Якщо true і placeholder не задано — створити прозорий 1x1 Texture2DArray автоматично")]
    public bool autoCreateTransparentPlaceholder = true;

    [Header("Events (Inspector)")]
    public UnityEvent OnPrepareCompleted;
    public UnityEvent OnFirstFrameReady;
    public UnityEvent OnStarted;
    public UnityEventInt OnChunkChanged;                 // newChunkIndex
    public UnityEventInt3 OnFrameChanged;                // global, chunk, intra
    public UnityEventInt OnNextChunkPreloadStarted;      // nextChunk
    public UnityEventInt OnNextChunkPreloadReady;        // nextChunk
    public UnityEventIntString OnNextChunkPreloadFailed; // nextChunk, error
    public UnityEvent OnLoop;
    public UnityEvent OnFinished;
    public UnityEventString OnError;
    public UnityEvent OnPlayEvent;
    public UnityEvent OnPauseEvent;
    public UnityEvent OnStopEvent;

    [Header("Events Tweaks")]
    [Tooltip("Чи викликати OnFrameChanged для кожного кадру (30 раз/сек при 30fps)")]
    public bool emitOnEveryFrame = false;

    // ===== C# events (для коду) =====
    public event Action PrepareCompleted;
    public event Action FirstFrameReady;
    public event Action Started;
    public event Action<int> ChunkChanged;
    public event Action<int,int,int> FrameChanged;
    public event Action<int> NextChunkPreloadStarted;
    public event Action<int> NextChunkPreloadReady;
    public event Action<int,string> NextChunkPreloadFailed;
    public event Action LoopTriggered;
    public event Action Finished;
    public event Action<string> Error;
    public event Action Played;
    public event Action Paused;
    public event Action Stopped;

    // ===== Runtime =====
    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;

    private string _addrCurrent;      // повна адреса поточного чанка
    private string _addrNext;         // повна адреса наступного чанка (якщо прелоадимо)
    private Texture2DArray _texCurrent;
    private Texture2DArray _texNext;

    private int _currentFrame;        // глобальний індекс кадру (0..)
    private float _accum;             // акумулятор часу
    private bool _playing = true;
    private bool _prepared = false;
    private bool _startedRaised = false;
    private bool _placeholderAutoCreated = false;

    // кеш значень для подій
    private int _lastEmittedGlobal = -1;
    private int _lastChunkIndex = -1;

    // ===== Public state =====
    public bool isPrepared => _prepared;
    public bool isPlaying  => _playing && _texCurrent != null;
    public bool isPaused   => !_playing && _prepared;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private async void OnEnable()
    {
        _renderer = _renderer ? _renderer : GetComponent<Renderer>();
        _mpb = _mpb ?? new MaterialPropertyBlock();

        // Плейсхолдер до завантаження
        EnsurePlaceholderAssigned();

        if (!manager) manager = FlipbookAssetManager.Instance;

        var set = manager ? manager.GetSet(setId) : null;
        if (set != null)
        {
            if (set.fps > 0) fps = set.fps;
            if (set.framesPerChunk > 0) framesPerChunk = set.framesPerChunk;
        }

        _currentFrame = 0;
        _accum = 0f;
        _prepared = false;
        _startedRaised = false;
        _lastEmittedGlobal = -1;
        _lastChunkIndex = -1;

        // Стартове завантаження першого чанка (0)
        int curChunk = 0;
        _addrCurrent = manager?.GetAddress(setId, curChunk);
        if (!string.IsNullOrEmpty(_addrCurrent))
        {
            _texCurrent = await manager.LoadChunkAsync(_addrCurrent);
            if (_texCurrent != null)
            {
                ApplyTexture(_texCurrent);
                _prepared = true;
                Raise(OnPrepareCompleted, PrepareCompleted);
                Raise(OnFirstFrameReady, FirstFrameReady);
            }
            else
            {
                RaiseError($"Failed to load first chunk: '{_addrCurrent}'");
            }
        }
        else
        {
            RaiseError("Address for first chunk is null/empty");
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            if (!string.IsNullOrEmpty(_addrNext)) manager.ReleaseChunk(_addrNext);
            if (!string.IsNullOrEmpty(_addrCurrent)) manager.ReleaseChunk(_addrCurrent);
        }
        _addrNext = null; _texNext = null;
        _addrCurrent = null; _texCurrent = null;

        // Повертаємо плейсхолдер
        if (placeholderArray != null) ApplyTexture(placeholderArray);
    }

    private void OnDestroy()
    {
        // Якщо плейсхолдер створили ми — знищимо
        if (_placeholderAutoCreated && placeholderArray != null)
        {
            if (Application.isPlaying) Destroy(placeholderArray);
            else DestroyImmediate(placeholderArray);
            placeholderArray = null;
        }
    }

    private void Update()
    {
        if (!_playing || _texCurrent == null || fps <= 0) return;

        // перший фактичний крок відтворення → OnStarted
        if (!_startedRaised)
        {
            _startedRaised = true;
            Raise(OnStarted, Started);
        }

        _accum += Time.deltaTime;
        float frameTime = 1f / Mathf.Max(1, fps);

        while (_accum >= frameTime)
        {
            _accum -= frameTime;
            StepFrame();
        }

        // Передаємо поточний кадр у шейдер
        int intra = _currentFrame % framesPerChunk;
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetInt(frameIndexProperty, intra);
        _renderer.SetPropertyBlock(_mpb);

        if (emitOnEveryFrame && _currentFrame != _lastEmittedGlobal)
        {
            var chunk = (_currentFrame / framesPerChunk);
            Raise(OnFrameChanged, FrameChanged, _currentFrame, chunk, intra);
            _lastEmittedGlobal = _currentFrame;
        }
    }

    private async void StepFrame()
    {
        _currentFrame++;

        int totalChunks = Mathf.Max(1, manager?.GetTotalChunks(setId) ?? 0);
        if (totalChunks <= 0 || framesPerChunk <= 0) return;

        // totalFrames (за припущенням паддингу) = totalChunks * framesPerChunk
        int totalFrames = totalChunks * framesPerChunk;

        int curChunk = (_currentFrame / framesPerChunk) % totalChunks;
        int intra = _currentFrame % framesPerChunk;

        // Прелоад наступного чанка
        if (framesPerChunk - intra <= preloadThresholdFrames)
        {
            int nextChunk = (curChunk + 1) % totalChunks;
            string wantedAddr = manager.GetAddress(setId, nextChunk);

            if (_addrNext != wantedAddr)
            {
                // звільнити попередній next, якщо був
                if (!string.IsNullOrEmpty(_addrNext)) manager.ReleaseChunk(_addrNext);

                _addrNext = wantedAddr;
                Raise(OnNextChunkPreloadStarted, NextChunkPreloadStarted, nextChunk);

                _texNext = await manager.LoadChunkAsync(_addrNext);
                if (_texNext != null)
                {
                    Raise(OnNextChunkPreloadReady, NextChunkPreloadReady, nextChunk);
                }
                else
                {
                    Raise(OnNextChunkPreloadFailed, NextChunkPreloadFailed, nextChunk, $"Failed to load '{_addrNext}'");
                }
            }
        }

        // Перемикання чанка, коли межа
        if (intra == 0)
        {
            // Перевірка лупа: якщо повернулися на кадр 0 (multiple of totalFrames)
            if (loop && totalFrames > 0 && (_currentFrame % totalFrames) == 0)
            {
                Raise(OnLoop, LoopTriggered);
            }

            // Якщо наступний завантажений — підміняємо
            if (_texNext != null && !string.IsNullOrEmpty(_addrNext))
            {
                // Звільнити поточний
                if (!string.IsNullOrEmpty(_addrCurrent)) manager.ReleaseChunk(_addrCurrent);

                // Стати на новий
                _texCurrent = _texNext;
                _addrCurrent = _addrNext;
                _texNext = null;
                _addrNext = null;
                ApplyCurrentTexture();

                if (curChunk != _lastChunkIndex)
                {
                    _lastChunkIndex = curChunk;
                    Raise(OnChunkChanged, ChunkChanged, curChunk);
                }
            }
            else
            {
                // Якщо next нема і loop вимкнено та це був останній чанк → стоп
                int lastChunkIndex = (totalChunks - 1);
                if (!loop && curChunk == lastChunkIndex)
                {
                    _playing = false;
                    Raise(OnFinished, Finished);
                }
            }
        }

        // Подія про зміну кадру (за потреби)
        if (emitOnEveryFrame && _currentFrame != _lastEmittedGlobal)
        {
            Raise(OnFrameChanged, FrameChanged, _currentFrame, curChunk, intra);
            _lastEmittedGlobal = _currentFrame;
        }
    }

    private void ApplyCurrentTexture()
    {
        if (_texCurrent == null) return;
        ApplyTexture(_texCurrent);
    }

    private void ApplyTexture(Texture2DArray tex)
    {
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetTexture(textureArrayProperty, tex);
        _mpb.SetInt(frameIndexProperty, 0);
        _renderer.SetPropertyBlock(_mpb);
    }

    private void EnsurePlaceholderAssigned()
    {
        if (placeholderArray == null && autoCreateTransparentPlaceholder)
        {
            var tmp = new Texture2DArray(1, 1, 1, TextureFormat.RGBA32, false, false);
            tmp.wrapMode = TextureWrapMode.Clamp;
            tmp.filterMode = FilterMode.Bilinear;
            tmp.anisoLevel = 1;

            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false, false);
            t.SetPixel(0, 0, new Color(0, 0, 0, 0));
            t.Apply(false, true);

            Graphics.CopyTexture(t, 0, 0, tmp, 0, 0);
            tmp.Apply(false, true);

            if (Application.isPlaying) Destroy(t); else DestroyImmediate(t);

            placeholderArray = tmp;
            _placeholderAutoCreated = true;
        }

        if (placeholderArray != null)
            ApplyTexture(placeholderArray);
    }

    // ===== Public controls =====
    public void Play()
    {
        bool wasPaused = !_playing && _prepared;
        _playing = true;
        if (wasPaused)
        {
            Raise(OnPlayEvent, Played);
        }
        else if (!_startedRaised && _prepared)
        {
            // Якщо ще не стартували, OnStarted підніметься в Update
        }
    }

    public void Pause()
    {
        if (!_prepared) return;
        if (_playing)
        {
            _playing = false;
            Raise(OnPauseEvent, Paused);
        }
    }

    public void Stop()
    {
        bool wasRunning = _prepared;
        _playing = false;
        _currentFrame = 0;
        _accum = 0f;
        _startedRaised = false;
        _lastEmittedGlobal = -1;
        _lastChunkIndex = -1;

        // повернути кадр 0 активного чанка (якщо є), або плейсхолдер
        if (_texCurrent != null) ApplyTexture(_texCurrent);
        else if (placeholderArray != null) ApplyTexture(placeholderArray);

        if (wasRunning) Raise(OnStopEvent, Stopped);
    }

    // ===== Helpers for raising events =====
    private void Raise(UnityEvent uevt, Action act)
    {
        try { uevt?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        try { act?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
    }
    private void Raise<T>(UnityEvent<T> uevt, Action<T> act, T arg)
    {
        try { uevt?.Invoke(arg); } catch (Exception e) { Debug.LogException(e); }
        try { act?.Invoke(arg); } catch (Exception e) { Debug.LogException(e); }
    }
    private void Raise<T1,T2>(UnityEvent<T1,T2> uevt, Action<T1,T2> act, T1 a, T2 b)
    {
        try { uevt?.Invoke(a,b); } catch (Exception e) { Debug.LogException(e); }
        try { act?.Invoke(a,b); } catch (Exception e) { Debug.LogException(e); }
    }
    // +++ ДОДАЙ цей перегруз (3 аргументи) +++
    private void Raise<T1, T2, T3>(
        UnityEvent<T1, T2, T3> uevt,
        Action<T1, T2, T3> act,
        T1 a, T2 b, T3 c)
    {
        try { uevt?.Invoke(a, b, c); } catch (Exception e) { Debug.LogException(e); }
        try { act?.Invoke(a, b, c); } catch (Exception e) { Debug.LogException(e); }
    }
    private void RaiseError(string msg)
    {
        Debug.LogError($"[FlipbookPlayer] {msg}");
        Raise(OnError, Error, msg);
    }

    // ===== UnityEvent types =====
    [Serializable] public class UnityEventInt      : UnityEvent<int> {}
    [Serializable] public class UnityEventInt3     : UnityEvent<int,int,int> {}
    [Serializable] public class UnityEventString   : UnityEvent<string> {}
    [Serializable] public class UnityEventIntString: UnityEvent<int,string> {}
}