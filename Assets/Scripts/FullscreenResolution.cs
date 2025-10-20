using UnityEngine;

public class FullscreenResolution : MonoBehaviour
{
    [Header("Бажана роздільна здатність (портрет)")]
    public int width = 1080;
    public int height = 1920;
    public int refreshRate = 60;

    [Header("Камера, яка має рендерити на другий дисплей")]
    public Camera targetCamera; // якщо пусто — візьме Camera.main

    [Header("Режим фулскріна")]
    public FullScreenMode mode = FullScreenMode.FullScreenWindow; // безрамковий фулскрін

    void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;

#if !UNITY_EDITOR
        // 1) встановлюємо фулскрін + роздільну
        Screen.fullScreenMode = mode;
        Screen.SetResolution(width, height, true);

        // 2) якщо є другий дисплей — активуємо та рендеримо туди
        if (Display.displays.Length > 1)
        {
            // активуємо другий дисплей з потрібною роздільною (якщо ОС дозволяє)
            Display.displays[1].Activate(width, height, refreshRate);

            // направляємо камеру на Display 2 (індекс 1)
            if (targetCamera != null)
                targetCamera.targetDisplay = 1;
        }
#endif
    }
}