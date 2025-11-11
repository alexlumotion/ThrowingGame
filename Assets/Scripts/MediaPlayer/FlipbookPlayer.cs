using System;
using System.Collections.Generic;
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

    [Header("Auto Control")]
    [Tooltip("Чи завантажувати перший чанк автоматично при активації компонента")]
    public bool autoLoadOnEnable = true;
    [Tooltip("Чи запускати програвання автоматично при активації компонента")]
    public bool autoPlayOnEnable = true;

    [Header("Debug Controls")]
    [Tooltip("Встанови true в інспекторі, щоб викликати Play() в Update (флаг скинеться автоматично)")]
    public bool debugTriggerPlay;
    [Tooltip("Встанови true в інспекторі, щоб викликати Pause() в Update (флаг скинеться автоматично)")]
    public bool debugTriggerPause;
    [Tooltip("Встанови true в інспекторі, щоб викликати Stop() в Update (флаг скинеться автоматично)")]
    public bool debugTriggerStop;

    [Header("Loading Tweaks")]
    [Tooltip("Мінімальна затримка перед первинним завантаженням (мс)")]
    public int initialLoadDelayMinMs = 0;
    [Tooltip("Максимальна затримка перед первинним завантаженням (мс)")]
    public int initialLoadDelayMaxMs = 0;

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
    private Task<Texture2DArray> _pendingNextChunkTask;

    private float _accum;             // акумулятор часу
    private bool _playing = true;
    private bool _prepared = false;
    private bool _startedRaised = false;
    private bool _placeholderAutoCreated = false;
    private bool _suppressAutoPlayOnEnable = false;
    private Task<bool> _prepareTask;
    private bool _isPreparing;
    private int _prepareRequestId;
    private bool _preserveCurrentTextureUntilPrepared;
    private int _currentChunkIndex;
    private int _frameInChunk;
    private int _framesInCurrentChunk;
    private int _framesInNextChunk;
    private int _nextChunkIndex = -1;
    private readonly Dictionary<int, int> _chunkFrameCounts = new Dictionary<int, int>();

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
        EnsureRendererReady();
        EnsurePlaceholderAssigned();
        EnsureManagerAssigned();
        ApplySetOverrides();

        ResetPlaybackState();
        ClearChunkReferences();

        _playing = ShouldAutoPlayOnEnable();

        if (!ShouldPrepareOnEnable(_playing))
        {
            return;
        }

        await DelayBeforeInitialPrepareAsync();

        if (!isActiveAndEnabled)
        {
            return;
        }

        bool prepared = await PrepareFirstChunkAsync();
        if (!prepared)
        {
            _playing = false;
        }
    }

    private Task<bool> PrepareFirstChunkAsync()
    {
        if (_prepared)
        {
            return Task.FromResult(true);
        }

        if (_isPreparing && _prepareTask != null)
        {
            return _prepareTask;
        }

        if (manager == null)
        {
            RaiseError("FlipbookAssetManager.Instance не задано");
            return Task.FromResult(false);
        }

        const int firstChunkIndex = 0;
        string address = manager.GetAddress(setId, firstChunkIndex);
        if (string.IsNullOrEmpty(address))
        {
            RaiseError("Address for first chunk is null/empty");
            return Task.FromResult(false);
        }

        _isPreparing = true;
        int requestId = ++_prepareRequestId;
        _prepareTask = PrepareFirstChunkInternalAsync(address, requestId, firstChunkIndex);
        return _prepareTask;
    }

    private async Task<bool> PrepareFirstChunkInternalAsync(string address, int requestId, int chunkIndex)
    {
        try
        {
            var tex = await manager.LoadChunkAsync(address, this);

            if (requestId != _prepareRequestId)
            {
                if (tex != null)
                {
                    manager.ReleaseChunk(address, this);
                }

                return false;
            }

            _currentChunkIndex = chunkIndex;
            _frameInChunk = 0;

            if (!isActiveAndEnabled)
            {
                if (tex != null)
                {
                    manager.ReleaseChunk(address, this);
                }

                return false;
            }

            if (tex != null)
            {
                ReleasePreservedChunkIfNeeded();

                _addrCurrent = address;
                _texCurrent = tex;
                _framesInCurrentChunk = RegisterChunkFrameCount(chunkIndex, tex);
                ApplyTexture(_texCurrent);
                _prepared = true;
                Raise(OnPrepareCompleted, PrepareCompleted);
                Raise(OnFirstFrameReady, FirstFrameReady);
                return true;
            }

            if (requestId == _prepareRequestId)
            {
                RaiseError($"Failed to load first chunk: '{address}'");
            }

            _framesInCurrentChunk = Mathf.Max(1, framesPerChunk);
            _addrCurrent = null;
            return false;
        }
        finally
        {
            _isPreparing = false;
            _prepareTask = null;
        }
    }

    private void OnDisable()
    {
        ++_prepareRequestId;
        ReleaseLoadedChunks();
        ClearChunkReferences();
        _chunkFrameCounts.Clear();
        _preserveCurrentTextureUntilPrepared = false;
        ResetPreparedFlags();
        ApplyPlaceholderTextureIfAvailable();
    }

    private void EnsureRendererReady()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    private void EnsureManagerAssigned()
    {
        if (!manager)
        {
            manager = FlipbookAssetManager.Instance;
        }
    }

    private void ApplySetOverrides()
    {
        var set = manager ? manager.GetSet(setId) : null;
        if (set == null) return;

        if (set.fps > 0) fps = set.fps;
        if (set.framesPerChunk > 0) framesPerChunk = set.framesPerChunk;
    }

    private void ResetPlaybackState()
    {
        _currentChunkIndex = 0;
        _frameInChunk = 0;
        _accum = 0f;
        _prepared = false;
        _startedRaised = false;
        _lastEmittedGlobal = -1;
        _lastChunkIndex = -1;
        _framesInCurrentChunk = 0;
        _framesInNextChunk = 0;
    }

    private void ClearChunkReferences()
    {
        _addrCurrent = null;
        _texCurrent = null;
        _addrNext = null;
        _texNext = null;
        _nextChunkIndex = -1;
        _framesInCurrentChunk = 0;
        _framesInNextChunk = 0;
        _pendingNextChunkTask = null;
    }

    private bool ShouldAutoPlayOnEnable() => autoPlayOnEnable && !_suppressAutoPlayOnEnable;

    private bool ShouldPrepareOnEnable(bool willAutoPlay) => autoLoadOnEnable || willAutoPlay;

    private void ReleaseLoadedChunks()
    {
        if (manager == null) return;

        if (!string.IsNullOrEmpty(_addrNext)) manager.ReleaseChunk(_addrNext, this);
        if (!string.IsNullOrEmpty(_addrCurrent)) manager.ReleaseChunk(_addrCurrent, this);
    }

    private void ReleaseNextChunkResources()
    {
        if (manager != null && !string.IsNullOrEmpty(_addrNext))
        {
            manager.ReleaseChunk(_addrNext, this);
        }

        _addrNext = null;
        _texNext = null;
        _nextChunkIndex = -1;
        _framesInNextChunk = 0;
        _pendingNextChunkTask = null;
    }

    private void ResetPreparedFlags()
    {
        _prepared = false;
        _playing = false;
    }

    private void ApplyPlaceholderTextureIfAvailable()
    {
        if (placeholderArray != null)
        {
            ApplyTexture(placeholderArray);
        }
    }

    private void ResetToFirstFrameIfCompleted()
    {
        if (!_prepared || loop)
        {
            return;
        }

        int totalChunks = Mathf.Max(1, manager?.GetTotalChunks(setId) ?? 0);
        if (totalChunks <= 0)
        {
            return;
        }

        bool atLastChunk = _currentChunkIndex >= totalChunks - 1;
        bool atLastFrame = _frameInChunk >= Mathf.Max(0, GetFramesInCurrentChunk() - 1);

        if (!atLastChunk || !atLastFrame)
        {
            return;
        }

        _currentChunkIndex = 0;
        _frameInChunk = 0;
        _accum = 0f;
        _startedRaised = false;
        _lastEmittedGlobal = -1;
        _lastChunkIndex = -1;
    }

    private async Task DelayBeforeInitialPrepareAsync()
    {
        int min = Mathf.Max(0, initialLoadDelayMinMs);
        int max = Mathf.Max(min, initialLoadDelayMaxMs);

        if (max <= 0)
        {
            return;
        }

        int delay = (max == min)
            ? min
            : UnityEngine.Random.Range(min, max + 1);

        if (delay <= 0)
        {
            return;
        }

        await Task.Delay(delay);
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
        if (debugTriggerPlay)
        {
            debugTriggerPlay = false;
            Play();
        }

        if (debugTriggerPause)
        {
            debugTriggerPause = false;
            Pause();
        }

        if (debugTriggerStop)
        {
            debugTriggerStop = false;
            Stop();
        }

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
        int currentChunkFrames = GetFramesInCurrentChunk();
        int intra = Mathf.Clamp(_frameInChunk, 0, Mathf.Max(0, currentChunkFrames - 1));
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetInt(frameIndexProperty, intra);
        _renderer.SetPropertyBlock(_mpb);

        if (emitOnEveryFrame)
        {
            int globalFrame = GetGlobalFrameIndex();
            if (globalFrame != _lastEmittedGlobal)
            {
                Raise(OnFrameChanged, FrameChanged, globalFrame, _currentChunkIndex, intra);
                _lastEmittedGlobal = globalFrame;
            }
        }
    }

    private async void StepFrame()
    {
        if (manager == null)
        {
            return;
        }

        int totalChunks = Mathf.Max(1, manager?.GetTotalChunks(setId) ?? 0);
        if (totalChunks <= 0) return;

        int framesInCurrent = GetFramesInCurrentChunk();
        _frameInChunk++;

        if (_frameInChunk >= framesInCurrent)
        {
            bool advanced = await TryAdvanceToNextChunkAsync(totalChunks, framesInCurrent);
            if (!advanced)
            {
                return;
            }

            framesInCurrent = GetFramesInCurrentChunk();
        }

        await MaybePreloadNextChunkAsync(totalChunks, framesInCurrent);
    }

    private async Task<bool> TryAdvanceToNextChunkAsync(int totalChunks, int previousChunkFrames)
    {
        bool wasLastChunk = (_currentChunkIndex >= totalChunks - 1);

        if (wasLastChunk)
        {
            if (!loop)
            {
                _playing = false;
                _frameInChunk = Mathf.Max(0, previousChunkFrames - 1);
                Raise(OnFinished, Finished);
                return false;
            }

            _currentChunkIndex = 0;
        }
        else
        {
            _currentChunkIndex++;
        }

        if (loop && wasLastChunk)
        {
            Raise(OnLoop, LoopTriggered);
        }

        bool switched = await SwitchToChunkAsync(_currentChunkIndex);
        if (!switched)
        {
            if (!wasLastChunk)
            {
                _currentChunkIndex = Mathf.Max(0, _currentChunkIndex - 1);
            }
            _frameInChunk = Mathf.Max(0, previousChunkFrames - 1);
            return false;
        }

        _frameInChunk = 0;

        if (_lastChunkIndex != _currentChunkIndex)
        {
            _lastChunkIndex = _currentChunkIndex;
            Raise(OnChunkChanged, ChunkChanged, _currentChunkIndex);
        }

        return true;
    }

    private async Task MaybePreloadNextChunkAsync(int totalChunks, int framesInCurrent)
    {
        if (manager == null)
        {
            return;
        }

        int framesRemaining = framesInCurrent - _frameInChunk;
        if (framesRemaining > preloadThresholdFrames)
        {
            return;
        }

        bool onLastChunkNoLoop = (!loop && _currentChunkIndex >= totalChunks - 1);
        if (onLastChunkNoLoop)
        {
            return;
        }

        int nextChunk = (_currentChunkIndex + 1) % totalChunks;
        string wantedAddr = manager.GetAddress(setId, nextChunk);
        if (string.IsNullOrEmpty(wantedAddr))
        {
            return;
        }

        if (_pendingNextChunkTask != null && !_pendingNextChunkTask.IsCompleted)
        {
            return;
        }

        if (_addrNext == wantedAddr && _texNext != null && _nextChunkIndex == nextChunk)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_addrNext) && _addrNext != wantedAddr)
        {
            manager.ReleaseChunk(_addrNext, this);
            _texNext = null;
            _addrNext = null;
            _framesInNextChunk = 0;
            _nextChunkIndex = -1;
        }

        _addrNext = wantedAddr;
        _nextChunkIndex = nextChunk;
        int requestId = _prepareRequestId;
        Raise(OnNextChunkPreloadStarted, NextChunkPreloadStarted, nextChunk);

        Task<Texture2DArray> loadTask = manager.LoadChunkAsync(_addrNext, this);
        _pendingNextChunkTask = loadTask;
        Texture2DArray tex;
        try
        {
            tex = await loadTask;
        }
        finally
        {
            if (_pendingNextChunkTask == loadTask)
            {
                _pendingNextChunkTask = null;
            }
        }

        if (requestId != _prepareRequestId)
        {
            if (tex != null)
            {
                manager.ReleaseChunk(_addrNext, this);
            }
            _texNext = null;
            _addrNext = null;
            _framesInNextChunk = 0;
            _nextChunkIndex = -1;
            return;
        }

        if (tex == null)
        {
            Raise(OnNextChunkPreloadFailed, NextChunkPreloadFailed, nextChunk, $"Failed to load '{_addrNext}'");
            _texNext = null;
            _addrNext = null;
            _framesInNextChunk = 0;
            _nextChunkIndex = -1;
            return;
        }

        if (!isActiveAndEnabled)
        {
            manager.ReleaseChunk(_addrNext, this);
            _texNext = null;
            _addrNext = null;
            _framesInNextChunk = 0;
            _nextChunkIndex = -1;
            return;
        }

        if (_nextChunkIndex != nextChunk || _addrNext != wantedAddr)
        {
            manager.ReleaseChunk(wantedAddr, this);
            return;
        }

        _texNext = tex;
        _framesInNextChunk = RegisterChunkFrameCount(nextChunk, tex);
        Raise(OnNextChunkPreloadReady, NextChunkPreloadReady, nextChunk);
    }

    private async Task<bool> SwitchToChunkAsync(int chunkIndex)
    {
        if (manager == null)
        {
            return false;
        }

        if (_texNext != null && _nextChunkIndex == chunkIndex && !string.IsNullOrEmpty(_addrNext))
        {
            ReleasePreservedChunkIfNeeded();

            if (!string.IsNullOrEmpty(_addrCurrent)) manager.ReleaseChunk(_addrCurrent, this);

            _texCurrent = _texNext;
            _addrCurrent = _addrNext;
            _texNext = null;
            _addrNext = null;
            if (_framesInNextChunk <= 0)
            {
                _framesInNextChunk = RegisterChunkFrameCount(chunkIndex, _texCurrent);
            }
            _framesInCurrentChunk = _framesInNextChunk;
            _framesInNextChunk = 0;
            _nextChunkIndex = -1;
            ApplyCurrentTexture();
            return true;
        }

        string address = manager.GetAddress(setId, chunkIndex);
        if (string.IsNullOrEmpty(address))
        {
            return false;
        }

        int requestId = _prepareRequestId;
        var tex = await manager.LoadChunkAsync(address, this);
        if (requestId != _prepareRequestId)
        {
            if (tex != null)
            {
                manager.ReleaseChunk(address, this);
            }
            return false;
        }

        if (tex == null)
        {
            RaiseError($"Failed to load chunk: '{address}'");
            return false;
        }

        ReleasePreservedChunkIfNeeded();

        if (!string.IsNullOrEmpty(_addrCurrent))
        {
            manager.ReleaseChunk(_addrCurrent, this);
        }

        _addrCurrent = address;
        _texCurrent = tex;
        _framesInCurrentChunk = RegisterChunkFrameCount(chunkIndex, tex);
        ApplyCurrentTexture();
        return true;
    }

    private void ApplyCurrentTexture()
    {
        if (_texCurrent == null) return;

        if (_framesInCurrentChunk <= 0)
        {
            _framesInCurrentChunk = RegisterChunkFrameCount(_currentChunkIndex, _texCurrent);
        }
        ApplyTexture(_texCurrent);
    }

    private void ApplyTexture(Texture2DArray tex)
    {
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetTexture(textureArrayProperty, tex);
        _mpb.SetInt(frameIndexProperty, 0);
        _renderer.SetPropertyBlock(_mpb);
    }

    private void ReleasePreservedChunkIfNeeded()
    {
        if (!_preserveCurrentTextureUntilPrepared)
        {
            return;
        }

        _preserveCurrentTextureUntilPrepared = false;

        if (manager != null && !string.IsNullOrEmpty(_addrCurrent))
        {
            manager.ReleaseChunk(_addrCurrent, this);
        }

        _addrCurrent = null;
        _texCurrent = null;
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
    public async void Play()
    {
        _suppressAutoPlayOnEnable = false;

        bool wasPaused = _prepared && !_playing;

        if (!_prepared)
        {
            bool prepared = await PrepareFirstChunkAsync();
            if (!prepared)
            {
                return;
            }

            wasPaused = false;
        }

        ResetToFirstFrameIfCompleted();

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
        StopInternal(null, false);
    }

    public void Stop(Action onStopped)
    {
        StopInternal(onStopped, false);
    }

    public void StopPreservingFrame(Action onStopped = null)
    {
        StopInternal(onStopped, true);
    }

    public void Preload(int targetSetId, Action onReady = null)
    {
        _ = PreloadAsync(targetSetId, onReady);
    }

    public async Task<bool> PreloadAsync(int targetSetId, Action onReady = null)
    {
        EnsureManagerAssigned();

        if (manager == null)
        {
            RaiseError("FlipbookAssetManager.Instance не задано");
            return false;
        }

        if (_isPreparing)
        {
            Debug.LogWarning("[FlipbookPlayer] Preload ignored: another prepare is in progress");
            return false;
        }

        bool alreadyPrepared = _prepared && setId == targetSetId && _texCurrent != null;
        if (alreadyPrepared)
        {
            InvokeSafely(onReady);
            return true;
        }

        _playing = false;
        _suppressAutoPlayOnEnable = true;

        ++_prepareRequestId;

        ReleaseLoadedChunks();
        ClearChunkReferences();
        ResetPlaybackState();
        _chunkFrameCounts.Clear();
        EnsurePlaceholderAssigned();
        ApplyPlaceholderTextureIfAvailable();

        setId = targetSetId;
        ApplySetOverrides();

        bool prepared = await PrepareFirstChunkAsync();

        if (prepared)
        {
            InvokeSafely(onReady);
        }

        return prepared;
    }

    public void Play(int targetSetId)
    {
        if (_prepared && setId == targetSetId && _texCurrent != null)
        {
            Play();
            return;
        }

        Preload(targetSetId, Play);
    }

    private void StopInternal(Action onStopped, bool preserveCurrentFrame)
    {
        bool wasRunning = _prepared;
        _playing = false;
        _suppressAutoPlayOnEnable = true;

        ++_prepareRequestId;

        ResetPlaybackState();

        if (preserveCurrentFrame && _texCurrent != null)
        {
            ReleaseNextChunkResources();
            _chunkFrameCounts.Clear();
            _preserveCurrentTextureUntilPrepared = true;
        }
        else
        {
            _preserveCurrentTextureUntilPrepared = false;
            ReleaseLoadedChunks();
            ClearChunkReferences();
            _chunkFrameCounts.Clear();
            EnsurePlaceholderAssigned();
            ApplyPlaceholderTextureIfAvailable();
        }

        ResetPreparedFlags();

        if (wasRunning) Raise(OnStopEvent, Stopped);
        InvokeSafely(onStopped);
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
        //Debug.LogError($"[FlipbookPlayer] {msg}");
        Raise(OnError, Error, msg);
    }

    private int RegisterChunkFrameCount(int chunkIndex, Texture2DArray tex)
    {
        int frameCount = Mathf.Max(1, tex != null ? tex.depth : framesPerChunk);
        _chunkFrameCounts[chunkIndex] = frameCount;
        return frameCount;
    }

    private int GetFramesInChunk(int chunkIndex)
    {
        if (chunkIndex < 0) return Mathf.Max(1, framesPerChunk);
        return _chunkFrameCounts.TryGetValue(chunkIndex, out var value)
            ? Mathf.Max(1, value)
            : Mathf.Max(1, framesPerChunk);
    }

    private int GetFramesInCurrentChunk()
    {
        if (_framesInCurrentChunk > 0) return _framesInCurrentChunk;
        _framesInCurrentChunk = GetFramesInChunk(_currentChunkIndex);
        return _framesInCurrentChunk;
    }

    private int GetGlobalFrameIndex()
    {
        int total = _frameInChunk;
        for (int i = 0; i < _currentChunkIndex; i++)
        {
            total += GetFramesInChunk(i);
        }
        return Mathf.Max(0, total);
    }

    private static void InvokeSafely(Action callback)
    {
        if (callback == null) return;
        try
        {
            callback();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // ===== UnityEvent types =====
    [Serializable] public class UnityEventInt      : UnityEvent<int> {}
    [Serializable] public class UnityEventInt3     : UnityEvent<int,int,int> {}
    [Serializable] public class UnityEventString   : UnityEvent<string> {}
    [Serializable] public class UnityEventIntString: UnityEvent<int,string> {}
}
