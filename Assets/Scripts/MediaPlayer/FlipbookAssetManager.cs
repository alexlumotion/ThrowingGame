using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// ==============================================================
// 1) ASSET MANAGER
//    • Зберігає набори (address template + кількість чанків)
//    • Кешує завантажені Texture2DArray c рефкаунтом
//    • Видає адреси й завантажує/звільняє чанки для плеєрів
// ==============================================================
[DefaultExecutionOrder(-5000)]
public class FlipbookAssetManager : MonoBehaviour
{
    [Serializable]
    public class SequenceSet
    {
        [Tooltip("Людська назва набору (для інспектора/зручного вибору)")] public string name = "Fish";
        [Tooltip("Шаблон адреси Addressables. Напр.: Fish/chunk_{0:000}.asset")] public string addressTemplate = "Fish/chunk_{0:000}.asset";
        [Tooltip("Кількість чанків у наборі")] public int totalChunks = 1;

        [Header("(Optional) Метадані — зручно тримати тут")]
        [Tooltip("FPS цього набору (за бажанням)")] public int fps = 30;
        [Tooltip("Кадрів у чанку (має збігатися зі способом білду)")] public int framesPerChunk = 120;
        [Tooltip("Чи починається нумерація з 0 (інакше з 1)")] public bool startAtZero = true;
    }

    [SerializeField] private List<SequenceSet> sets = new List<SequenceSet>();

    // Runtime-кеш: адреса → завантажений asset + refcount
    private class CacheEntry
    {
        public AsyncOperationHandle<Texture2DArray> handle;
        public Texture2DArray asset;
        public int refCount;
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

    // ---------- API конфіг ----------
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

    // ---------- Завантаження з кешем і рефкаунтом ----------
    public async Task<Texture2DArray> LoadChunkAsync(string address)
    {
        if (string.IsNullOrEmpty(address)) return null;

        // Якщо вже в кеші — збільшити ref і повернути
        if (_cache.TryGetValue(address, out var entry))
        {
            entry.refCount++;
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
            return entry.asset; // може бути null, якщо ще не довантажилось
        }

        // Нове завантаження
        var handle = Addressables.LoadAssetAsync<Texture2DArray>(address);
        var newEntry = new CacheEntry { handle = handle, refCount = 1 };
        _cache[address] = newEntry;
        try
        {
            var tex = await handle.Task; // await дозволений у 2022.3
            newEntry.asset = tex;
            return tex;
        }
        catch (Exception e)
        {
            Debug.LogError($"FlipbookAssetManager: failed to load '{address}' — {e.Message}");
            // Провал — зняти з кешу і релізнути хендл
            if (_cache.TryGetValue(address, out var ent))
            {
                _cache.Remove(address);
                if (ent.handle.IsValid()) Addressables.Release(ent.handle);
            }
            return null;
        }
    }

    public void ReleaseChunk(string address)
    {
        if (string.IsNullOrEmpty(address)) return;
        if (!_cache.TryGetValue(address, out var entry)) return;

        entry.refCount = Mathf.Max(0, entry.refCount - 1);
        if (entry.refCount == 0)
        {
            if (entry.handle.IsValid()) Addressables.Release(entry.handle);
            _cache.Remove(address);
        }
    }
}