using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Тестер для ручного завантаження/вивантаження анімацій риб (AnimatorOverrideController + залежні текстури).
/// </summary>
public class FishAnimationBundleTester : MonoBehaviour
{
    [Serializable]
    public class AnimationEntry
    {
        [Tooltip("Зручна назва (відображається в UI)")]
        public string displayName;

        [Tooltip("AnimatorOverrideController, який лежить у потрібному бандлі (його завантаження підтягне всі спрайти).")]
        public AssetReferenceAnimatorOverrideController overrideReference;

        [NonSerialized] public AsyncOperationHandle<AnimatorOverrideController>? handle;
        [NonSerialized] public State state = State.Unloaded;
        [NonSerialized] public bool desiredLoaded;

        public enum State
        {
            Unloaded,
            Loading,
            Loaded,
            Unloading,
            Failed
        }
    }

    [Header("Animations")]
    [SerializeField] private List<AnimationEntry> animations = new();

    [Header("UI")]
    [SerializeField] private bool showWindow = true;
    [SerializeField] private Vector2 windowOffset = new Vector2(12f, 12f);
    [SerializeField] private float windowWidth = 360f;
    [SerializeField] private string windowTitle = "Fish Animation Bundles";

    private GUIStyle headerStyle;

    private void OnGUI()
    {
        if (!showWindow)
            return;

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        }

        GUILayout.BeginArea(new Rect(windowOffset.x, windowOffset.y, windowWidth, Screen.height - windowOffset.y * 2f), GUI.skin.window);
        GUILayout.Label(windowTitle, headerStyle);

        foreach (var entry in animations)
        {
            DrawEntry(entry);
        }

        if (GUILayout.Button("Unload All"))
        {
            foreach (var entry in animations)
            {
                if (entry != null && entry.state == AnimationEntry.State.Loaded)
                    StartCoroutine(UnloadEntry(entry));
            }
        }

        GUILayout.EndArea();
    }

    private void DrawEntry(AnimationEntry entry)
    {
        if (entry == null)
            return;

        GUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label(!string.IsNullOrEmpty(entry.displayName) ? entry.displayName : "(Unnamed Animation)");
        GUILayout.Label($"State: {entry.state}");

        string key = entry.overrideReference != null && entry.overrideReference.RuntimeKeyIsValid()
            ? entry.overrideReference.RuntimeKey.ToString()
            : "<missing>";
        GUILayout.Label($"Override: {key}");

        GUILayout.BeginHorizontal();
        GUI.enabled = entry.state == AnimationEntry.State.Unloaded || entry.state == AnimationEntry.State.Failed;
        if (GUILayout.Button("Load"))
        {
            entry.desiredLoaded = true;
            StartCoroutine(LoadEntry(entry));
        }
        GUI.enabled = entry.state == AnimationEntry.State.Loaded;
        if (GUILayout.Button("Unload"))
        {
            entry.desiredLoaded = false;
            StartCoroutine(UnloadEntry(entry));
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private System.Collections.IEnumerator LoadEntry(AnimationEntry entry)
    {
        if (entry.overrideReference == null || !entry.overrideReference.RuntimeKeyIsValid())
        {
            Debug.LogWarning("[FishAnimationBundleTester] Override reference missing or invalid.");
            entry.state = AnimationEntry.State.Failed;
            entry.desiredLoaded = false;
            yield break;
        }

        if (entry.state == AnimationEntry.State.Loading || entry.state == AnimationEntry.State.Loaded)
            yield break;

        entry.state = AnimationEntry.State.Loading;

        var handle = entry.overrideReference.LoadAssetAsync<AnimatorOverrideController>();
        entry.handle = handle;

        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            entry.state = AnimationEntry.State.Loaded;
        }
        else
        {
            Debug.LogWarning($"[FishAnimationBundleTester] Failed to load override: {keyFor(entry)}");
            entry.state = AnimationEntry.State.Failed;
            entry.desiredLoaded = false;
            if (entry.handle.HasValue)
            {
                Addressables.Release(entry.handle.Value);
                entry.handle = null;
            }
        }
    }

    private System.Collections.IEnumerator UnloadEntry(AnimationEntry entry)
    {
        if (entry.state == AnimationEntry.State.Unloaded || entry.state == AnimationEntry.State.Unloading)
            yield break;

        entry.state = AnimationEntry.State.Unloading;

        if (entry.handle.HasValue)
        {
            Addressables.Release(entry.handle.Value);
            entry.handle = null;
        }

        yield return null;
        entry.state = AnimationEntry.State.Unloaded;
    }

    private void OnDestroy()
    {
        foreach (var entry in animations)
        {
            if (entry != null && entry.handle.HasValue)
            {
                Addressables.Release(entry.handle.Value);
                entry.handle = null;
            }
        }
    }

    private string keyFor(AnimationEntry entry)
    {
        return entry.overrideReference != null && entry.overrideReference.RuntimeKeyIsValid()
            ? entry.overrideReference.RuntimeKey.ToString()
            : "<missing>";
    }
}

