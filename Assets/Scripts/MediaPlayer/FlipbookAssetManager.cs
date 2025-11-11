#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[DefaultExecutionOrder(-5000)]
public class FlipbookAssetManager : MonoBehaviour
{
    // =======================
    // Конфіг наборів
    // =======================
    [Serializable]
    public class SequenceSet
    {
        [Tooltip("Людська назва набору (для інспектора/зручного вибору)")]
        public string name = "Fish";

        [Tooltip("Шаблон адреси Addressables. Напр.: Assets/FlipbookChunks/Fishes_chunk{0:00}_2048x1024_DXT5.asset")]
        public string addressTemplate = "Assets/FlipbookChunks/Fishes_chunk{0:00}_2048x1024_DXT5.asset";

        [Tooltip("Кількість чанків у наборі")]
        public int totalChunks = 1;

        [Header("(Optional) Метадані — зручно тримати тут")]
        [Tooltip("FPS цього набору (за бажанням)")] public int fps = 30;
        [Tooltip("Кадрів у чанку (має збігатися зі способом білду)")] public int framesPerChunk = 120;
        [Tooltip("Чи починається нумерація з 0 (інакше з 1)")] public bool startAtZero = true;
    }

    [SerializeField] private List<SequenceSet> sets = new List<SequenceSet>();
    public int SetCount => sets.Count;
    public SequenceSet GetSet(int setId) => (setId >= 0 && setId < sets.Count) ? sets[setId] : null;

    public string GetAddress(int setId, int chunkIndex)
    {
        var s = GetSet(setId);
        if (s == null) return null;
        int logicalIndex = s.startAtZero ? chunkIndex : (chunkIndex + 1);
        return string.Format(s.addressTemplate, logicalIndex);
    }

    public int GetTotalChunks(int setId) => GetSet(setId)?.totalChunks ?? 0;

    // =======================
    // Debug / лог кешу
    // =======================
    [Serializable]
    public class DebugOptions
    {
        [Tooltip("Якщо > 0, виводити зведення кешу раз на N секунд")]
        public float autoLogIntervalSec = 0f;

        [Tooltip("Логувати повний список елементів кешу (інакше тільки заголовок)")]
        public bool detailed = true;

        [Tooltip("Трекати власників (плеєри/будь-які теги), що утримують ресурси")]
        public bool trackOwners = true;
    }

    [Header("Debug / Cache logging")]
    [SerializeField] private DebugOptions debug = new DebugOptions();

    private class CacheEntry
    {
        public AsyncOperationHandle<Texture2DArray> handle;
        public Texture2DArray asset;
        public int refCount;
        public HashSet<string> owners;   // хто тримає (опційно)
        public DateTime lastAccessUtc;
    }

    private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>(128);

    public static FlipbookAssetManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogWarning("FlipbookAssetManager: duplicate instance destroyed");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (debug.autoLogIntervalSec > 0f)
            StartCoroutine(AutoLog());
    }

    private System.Collections.IEnumerator AutoLog()
    {
        var wait = new WaitForSecondsRealtime(Mathf.Max(0.5f, debug.autoLogIntervalSec));
        for (;;)
        {
            yield return wait;
            LogCacheSummary(debug.detailed);
        }
    }

    // =======================
    // Публічний логер
    // =======================
    public void LogCacheSummary(bool detailed = true)
    {
        int count = _cache.Count;
        long totalBytes = 0;
        foreach (var kv in _cache)
            totalBytes += EstimateBytes(kv.Value.asset);

        Debug.Log($"[FlipbookAssetManager] CACHE: {count} items, ~{HumanMB(totalBytes)}");

        if (!detailed) return;

        int i = 0;
        foreach (var kv in _cache)
        {
            var e = kv.Value;
            string mb = HumanMB(EstimateBytes(e.asset));
            string owners = (e.owners != null && e.owners.Count > 0) ? string.Join(",", e.owners) : "-";
            Debug.Log($"  {++i:00}. ref={e.refCount}  size≈{mb}  last={e.lastAccessUtc:HH:mm:ss}  addr='{kv.Key}'  owners=[{owners}]");
        }
    }

    // =======================
    // API завантаження / звільнення
    // =======================

    // Старий сумісний API (без ownerTag)
    public Task<Texture2DArray> LoadChunkAsync(string address) => LoadChunkAsync(address, ownerTag: null);

    // Новий: із ownerTag (будь-що; для логу перетворюється на рядок)
    public async Task<Texture2DArray> LoadChunkAsync(string address, object ownerTag)
    {
        if (string.IsNullOrEmpty(address)) return null;

        if (_cache.TryGetValue(address, out var entry))
        {
            entry.refCount++;
            entry.lastAccessUtc = DateTime.UtcNow;
            if (debug.trackOwners) (entry.owners ??= new HashSet<string>()).Add(OwnerTag(ownerTag));

            if (entry.handle.IsValid())
            {
                if (entry.handle.IsDone)
                {
                    entry.asset = entry.handle.Result;
                    return entry.asset;
                }
                else
                {
                    await entry.handle.Task;
                    entry.asset = entry.handle.Result;
                    return entry.asset;
                }
            }
            return entry.asset;
        }

        var handle = Addressables.LoadAssetAsync<Texture2DArray>(address);
        var newEntry = new CacheEntry
        {
            handle = handle,
            refCount = 1,
            owners = debug.trackOwners ? new HashSet<string>() : null,
            lastAccessUtc = DateTime.UtcNow
        };
        if (debug.trackOwners) newEntry.owners.Add(OwnerTag(ownerTag));

        _cache[address] = newEntry;

        try
        {
            var tex = await handle.Task;
            newEntry.asset = tex;
            return tex;
        }
        catch (Exception e)
        {
            Debug.LogError($"FlipbookAssetManager: failed to load '{address}' — {e.Message}");
            if (_cache.TryGetValue(address, out var ent))
            {
                _cache.Remove(address);
                if (ent.handle.IsValid()) Addressables.Release(ent.handle);
            }
            return null;
        }
    }

    // Старий сумісний API (без ownerTag)
    public void ReleaseChunk(string address) => ReleaseChunk(address, ownerTag: null);

    // Новий: із ownerTag
    public void ReleaseChunk(string address, object ownerTag)
    {
        if (string.IsNullOrEmpty(address)) return;
        if (!_cache.TryGetValue(address, out var entry)) return;

        entry.refCount = Mathf.Max(0, entry.refCount - 1);
        entry.lastAccessUtc = DateTime.UtcNow;
        if (debug.trackOwners && ownerTag != null)
            entry.owners?.Remove(OwnerTag(ownerTag));

        if (entry.refCount == 0)
        {
            if (entry.handle.IsValid()) Addressables.Release(entry.handle);
            _cache.Remove(address);
        }
    }

    // (Опціонально) форс-прибирання всього непотрібного
    public void TrimAllUnused()
    {
        var toRemove = new List<string>();
        foreach (var kv in _cache)
        {
            if (kv.Value.refCount == 0)
            {
                if (kv.Value.handle.IsValid()) Addressables.Release(kv.Value.handle);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var k in toRemove) _cache.Remove(k);
        Debug.Log($"[FlipbookAssetManager] TrimAllUnused: removed {toRemove.Count} items");
    }

    // =======================
    // Допоміжні утиліти
    // =======================
    private static long EstimateBytes(Texture2DArray tex)
    {
        if (tex == null) return 0;
        int w = tex.width, h = tex.height, d = tex.depth;
        float bpp = 4f; // fallback
        switch (tex.format)
        {
            case TextureFormat.DXT5:
            case TextureFormat.BC7:
                bpp = 1f; // ~8bpp ≈ 1 byte/px
                break;
            case TextureFormat.RGBA32:
                bpp = 4f;
                break;
        }
        return (long)((double)w * h * d * bpp);
    }

    private static string HumanMB(long bytes) => $"{bytes / (1024.0 * 1024.0):F1} MB";

    private static string OwnerTag(object owner)
    {
        if (owner == null) return "null";
        if (owner is UnityEngine.Object uo) return $"{uo.GetType().Name}#{uo.GetInstanceID()}";
        return owner.ToString();
    }
}
#endif