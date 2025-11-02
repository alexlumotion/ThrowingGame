using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Потокове відтворення анімацій риб за допомогою Addressables (аналог FishVideoController).
/// Тримає в пам'яті лише поточний та наступний кліп.
/// </summary>
public class FishAnimationController : MonoBehaviour
{
    public enum Direction { LeftToRight, RightToLeft }

    [Header("References")]
    [SerializeField] private Animator animator;
    [Tooltip("Базовий контролер з єдиним placeholder-кліпом.")]
    [SerializeField] private RuntimeAnimatorController baseController;
    [Tooltip("Placeholder-кліп, який підмінюється в Override Controller.")]
    [SerializeField] private AnimationClip placeholderClip;

    [Header("Addressable Overrides")]
    [SerializeField] private AssetReferenceAnimatorOverrideController[] leftToRightOverrides;
    [SerializeField] private AssetReferenceAnimatorOverrideController[] rightToLeftOverrides;
    [SerializeField] private AssetReferenceAnimatorOverrideController[] interludeOverrides;

    [Header("Playback")]
    [SerializeField] private bool autoPlayOnStart = true;
    [SerializeField] private Direction startDirection = Direction.LeftToRight;
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Header("Interlude (кожні N секунд)")]
    [Tooltip("Інтервал між інтерлюдіями в секундах (за замовчуванням 5 хв = 300с).")]
    [Min(1f)]
    [SerializeField] private float interludeIntervalSeconds = 300f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugHotkeys = false;
    [SerializeField] private KeyCode debugNextKey = KeyCode.N;

    private Direction currentDirection;
    private AssetReferenceAnimatorOverrideController lastPlayedReference;

    // Інтерлюдія/таймер
    private float nextInterludeAt = Mathf.Infinity;
    private bool interludePending = false;
    private bool isInterludePlaying = false;

    // Поточний кліп
    private AnimationClip currentClip;
    private float currentClipStartTime;
    private float currentClipDuration;
    private float currentClipFrameRate = 60f;
    private bool isClipActive = false;
    private bool clipEndHandled = false;
    private bool isLoading = false;

    // Addressables handles
    private AsyncOperationHandle<AnimatorOverrideController> currentHandle;
    private bool hasCurrentHandle = false;
    private AssetReferenceAnimatorOverrideController currentReference;

    private static readonly List<KeyValuePair<AnimationClip, AnimationClip>> overrideBuffer = new();

    private void Reset()
    {
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            baseController = animator.runtimeAnimatorController;
        }
    }

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!animator)
        {
            Debug.LogError("[FishAnimationController] Animator reference is missing.");
            enabled = false;
            return;
        }

        if (!baseController)
            baseController = animator.runtimeAnimatorController;

        if (!baseController)
            Debug.LogWarning("[FishAnimationController] Base controller is not assigned.");
    }

    private void Start()
    {
        currentDirection = startDirection;
        ScheduleNextInterlude();

        if (baseController)
            animator.runtimeAnimatorController = baseController;

        if (autoPlayOnStart)
            PlayNext(currentDirection);
    }

    private void OnDestroy()
    {
        if (hasCurrentHandle)
        {
            Addressables.Release(currentHandle);
            hasCurrentHandle = false;
        }
    }

    private void Update()
    {
        if (!isClipActive || currentClip == null)
            return;

        float elapsed = Time.time - currentClipStartTime;
        float remaining = currentClipDuration - elapsed;

        if (!clipEndHandled && remaining <= 0f)
        {
            clipEndHandled = true;
            HandleNextOnEnd();
        }

        if (enableDebugHotkeys && Input.GetKeyDown(debugNextKey))
        {
            clipEndHandled = true;
            HandleNextOnEnd();
        }
    }

    public void PlayDirection(Direction dir)
    {
        currentDirection = dir;
        if (!isInterludePlaying)
            PlayNext(currentDirection);
    }

    public void ToggleDirection()
    {
        currentDirection = currentDirection == Direction.LeftToRight ? Direction.RightToLeft : Direction.LeftToRight;

        if (!isInterludePlaying)
            PlayNext(currentDirection);
    }

    public void SetInterludeIntervalSeconds(float seconds)
    {
        interludeIntervalSeconds = Mathf.Max(1f, seconds);
        ScheduleNextInterlude();
    }

    public void RequestInterlude()
    {
        interludePending = true;
    }

    private void HandleNextOnEnd()
    {
        isClipActive = false;

        if (isInterludePlaying)
        {
            isInterludePlaying = false;
            ScheduleNextInterlude();
            PlayNext(currentDirection);
            return;
        }

        if (interludePending || Time.time >= nextInterludeAt)
        {
            interludePending = false;
            PlayInterlude();
            return;
        }

        currentDirection = currentDirection == Direction.LeftToRight ? Direction.RightToLeft : Direction.LeftToRight;
        PlayNext(currentDirection);
    }

    private void PlayInterlude()
    {
        if (isLoading)
            return;

        var reference = PickReference(interludeOverrides);
        if (reference == null)
        {
            Debug.LogWarning("[FishAnimationController] No interlude overrides assigned. Continue normal playback.");
            ScheduleNextInterlude();
            PlayNext(currentDirection);
            return;
        }

        StartCoroutine(LoadAndApplyRoutine(reference, true, currentDirection));
    }

    private void PlayNext(Direction dir)
    {
        if (isLoading)
            return;

        var pool = GetPool(dir);
        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning($"[FishAnimationController] No overrides for direction {dir}.");
            return;
        }

        var reference = PickReference(pool);
        if (reference == null)
        {
            Debug.LogWarning($"[FishAnimationController] Failed to pick override for direction {dir}.");
            return;
        }

        StartCoroutine(LoadAndApplyRoutine(reference, false, dir));
    }

    private IEnumerator LoadAndApplyRoutine(AssetReferenceAnimatorOverrideController reference, bool isForInterlude, Direction direction)
    {
        if (!IsReferenceValid(reference))
        {
            Debug.LogWarning("[FishAnimationController] Invalid or empty AssetReference provided.");
            yield break;
        }

        if (isLoading)
            yield break;

        isLoading = true;

        var handle = reference.LoadAssetAsync<AnimatorOverrideController>();

        if (!handle.IsDone)
            yield return handle;

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogWarning($"[FishAnimationController] Failed to load AnimatorOverrideController ({reference.RuntimeKey}).");
            Addressables.Release(handle);
            isLoading = false;

            if (isForInterlude)
            {
                ScheduleNextInterlude();
                PlayNext(currentDirection);
            }
            else
            {
                PlayNext(direction);
            }
            yield break;
        }

        ApplyLoadedOverride(handle, reference, isForInterlude, direction);
        isLoading = false;
    }

    private void ApplyLoadedOverride(AsyncOperationHandle<AnimatorOverrideController> handle,
                                     AssetReferenceAnimatorOverrideController reference,
                                     bool isForInterlude,
                                     Direction direction)
    {
        var overrideController = handle.Result;

        var previousHandle = currentHandle;
        bool hadPreviousHandle = hasCurrentHandle;

        currentHandle = handle;
        hasCurrentHandle = true;
        currentReference = reference;

        isInterludePlaying = isForInterlude;
        currentDirection = direction;

        animator.runtimeAnimatorController = overrideController;
        animator.Play(0, 0, 0f);

        currentClip = ResolveClip(overrideController);
        if (currentClip != null)
        {
            currentClipDuration = currentClip.length;
            currentClipFrameRate = Mathf.Max(1f, currentClip.frameRate);
            currentClipStartTime = Time.time;
            isClipActive = true;
            clipEndHandled = false;
        }
        else
        {
            Debug.LogWarning("[FishAnimationController] Override controller does not provide a clip. Skipping to the next one.");
            isClipActive = false;
            clipEndHandled = true;
            HandleNextOnEnd();
        }

        lastPlayedReference = reference;

        if (hadPreviousHandle)
        {
            Addressables.Release(previousHandle);
        }
    }

    private AssetReferenceAnimatorOverrideController[] GetPool(Direction direction)
    {
        return direction == Direction.LeftToRight ? leftToRightOverrides : rightToLeftOverrides;
    }

    private AssetReferenceAnimatorOverrideController PickReference(AssetReferenceAnimatorOverrideController[] pool)
    {
        if (pool == null || pool.Length == 0)
            return null;

        AssetReferenceAnimatorOverrideController fallback = null;
        int attempts = 0;
        int maxAttempts = Mathf.Max(pool.Length * 2, 8);

        while (attempts < maxAttempts)
        {
            var candidate = pool[Random.Range(0, pool.Length)];
            attempts++;

            if (!IsReferenceValid(candidate))
                continue;

            fallback ??= candidate;

            if (!avoidImmediateRepeat || lastPlayedReference == null || !AreSameReference(candidate, lastPlayedReference))
                return candidate;
        }

        return IsReferenceValid(fallback) ? fallback : null;
    }

    private AnimationClip ResolveClip(AnimatorOverrideController overrideController)
    {
        if (overrideController == null)
            return null;

        if (placeholderClip)
        {
            var clip = overrideController[placeholderClip];
            if (clip)
                return clip;
        }

        overrideBuffer.Clear();
        overrideController.GetOverrides(overrideBuffer);
        for (int i = 0; i < overrideBuffer.Count; i++)
        {
            if (overrideBuffer[i].Value)
                return overrideBuffer[i].Value;
        }

        return null;
    }

    private void ScheduleNextInterlude()
    {
        nextInterludeAt = Time.time + Mathf.Max(1f, interludeIntervalSeconds);
        interludePending = false;
    }

    private bool IsReferenceValid(AssetReferenceAnimatorOverrideController reference)
    {
        return reference != null && reference.RuntimeKeyIsValid();
    }

    private static bool AreSameReference(AssetReference a, AssetReference b)
    {
        if (a == null || b == null)
            return false;

        if (!a.RuntimeKeyIsValid() || !b.RuntimeKeyIsValid())
            return false;

        return Equals(a.RuntimeKey, b.RuntimeKey);
    }

}

[System.Serializable]
public class AssetReferenceAnimatorOverrideController : AssetReferenceT<AnimatorOverrideController>
{
    public AssetReferenceAnimatorOverrideController(string guid) : base(guid) { }
}
