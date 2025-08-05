using UnityEngine;
using System.Collections;

/// <summary>
/// Менеджер для базової оптимізації продуктивності
/// Працює як для клієнта, так і для сервера
/// </summary>
public class OptimizationManager : MonoBehaviour
{
    [Header("Налаштування оптимізації")]
    [Tooltip("Цільовий FPS (кадрів в секунду)")]
    public int targetFPS = 30;

    [Tooltip("Інтервал очищення невикористаних ресурсів (у секундах)")]
    public float unloadInterval = 1800f; // 30 хвилин

    void Awake()
    {
        Debug.Log("⚙️ OptimizationManager: запуск оптимізації");

        Application.targetFrameRate = targetFPS;
        QualitySettings.vSyncCount = 0;

        Debug.Log($"📉 FPS обмежено до {targetFPS}, VSync вимкнено");
    }

    void Start()
    {
        StartCoroutine(UnloadRoutine());
    }

    IEnumerator UnloadRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(unloadInterval);
            Debug.Log("🧹 Очищення невикористаних ресурсів");
            Resources.UnloadUnusedAssets();
        }
    }
} 
