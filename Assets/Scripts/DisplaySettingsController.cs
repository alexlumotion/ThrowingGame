using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple controller to choose which display to render to (Display 1 or 2)
/// and whether to use fullscreen or a fixed window resolution.
/// Attach this to a GameObject in your startup scene.
/// </summary>
public class DisplaySettingsController : MonoBehaviour
{
    public enum TargetDisplay
    {
        Display1 = 0,
        Display2 = 1
    }

    [Header("Display")] 
    [Tooltip("Select target display. If the display is not available, will fallback to Display 1.")] 
    public TargetDisplay display = TargetDisplay.Display1;

    [Tooltip("Activate additional displays on start (required to use Display 2).")]
    public bool activateDisplaysOnStart = true;

    [Tooltip("Retarget all cameras to the selected display on start.")]
    public bool retargetAllCameras = true;

    [Header("Resolution")]
    [Tooltip("If enabled, uses fullscreen on the selected display. If disabled, uses the fixed resolution below in a window.")]
    public bool fullscreen = true;

    [Tooltip("Fullscreen mode to use when 'fullscreen' is enabled.")]
    public FullScreenMode fullscreenMode = FullScreenMode.FullScreenWindow;

    [Tooltip("Fixed resolution for windowed mode (used when 'fullscreen' is disabled).")]
    public Vector2Int fixedResolution = new Vector2Int(1920, 1080);

    [Tooltip("Apply the selected display and resolution at Start().")]
    public bool applyOnStart = true;

    [Header("Window Routing")]
    [Tooltip("Attempts to place the main game window on the selected display at startup (Unity 2022.2+).")]
    public bool moveMainWindowToDisplay = true;

    void Start()
    {
        if (activateDisplaysOnStart)
        {
            ActivateAllAvailableDisplays();
        }

        int targetIndex = GetSafeDisplayIndex();

        if (retargetAllCameras)
        {
            SetCamerasTargetDisplay(targetIndex);
        }

        if (applyOnStart)
        {
            // Try to place the main player window on the target display first
            if (moveMainWindowToDisplay)
            {
                TryMoveMainWindowToDisplay(targetIndex);
            }

            ApplyResolutionForDisplay(targetIndex);
        }
    }

    /// <summary>
    /// Activates all connected displays so that Display 2 can be used.
    /// </summary>
    public void ActivateAllAvailableDisplays()
    {
        // Display 0 (primary) is already active. Others may need activation.
        for (int i = 1; i < Display.displays.Length; i++)
        {
            try
            {
                Display.displays[i].Activate();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not activate display {i + 1}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Attempts to move the main player window to the specified display.
    /// Uses Screen.MoveMainWindowTo on Unity 2022.2+, otherwise no-op.
    /// </summary>
    public void TryMoveMainWindowToDisplay(int displayIndex)
    {
        displayIndex = Mathf.Clamp(displayIndex, 0, Mathf.Max(0, Display.displays.Length - 1));

#if UNITY_2022_2_OR_NEWER
        try
        {
            // Unity 2022.2+ API expects DisplayInfo and a window position (Vector2Int)
            var displayInfos = new List<DisplayInfo>();
            Screen.GetDisplayLayout(displayInfos);
            if (displayIndex < displayInfos.Count)
            {
                var targetInfo = displayInfos[displayIndex];
                // Place window at the top-left corner of the target display
                Screen.MoveMainWindowTo(in targetInfo, Vector2Int.zero);
            }
            else
            {
                Debug.LogWarning($"Target display index {displayIndex} not found in display layout (count: {displayInfos.Count}).");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"MoveMainWindowTo not available or failed: {e.Message}");
        }
#else
        // Older Unity versions don't provide a reliable cross-platform API to move the main window.
        // On such versions, the content can still be rendered to Display 2 by retargeting cameras
        // and activating multi-display. The main window may remain on the primary monitor.
#endif
    }

    /// <summary>
    /// Applies fullscreen or windowed resolution according to the current settings.
    /// </summary>
    public void ApplyResolutionForDisplay(int displayIndex)
    {
        displayIndex = Mathf.Clamp(displayIndex, 0, Mathf.Max(0, Display.displays.Length - 1));

        if (fullscreen)
        {
            // Use the native resolution of the target display when going fullscreen
            int w = Display.displays[displayIndex].systemWidth;
            int h = Display.displays[displayIndex].systemHeight;
            Screen.SetResolution(w, h, fullscreenMode);
        }
        else
        {
            int w = Mathf.Max(64, fixedResolution.x);
            int h = Mathf.Max(64, fixedResolution.y);
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
        }
    }

    /// <summary>
    /// Assigns all cameras to render on the selected display.
    /// </summary>
    public void SetCamerasTargetDisplay(int displayIndex)
    {
        displayIndex = Mathf.Clamp(displayIndex, 0, 7); // Unity supports up to 8 displays
        Camera[] cams = Camera.allCameras;
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null)
            {
                cams[i].targetDisplay = displayIndex;
            }
        }
    }

    /// <summary>
    /// Returns the selected display index but falls back to 0 if not available.
    /// </summary>
    private int GetSafeDisplayIndex()
    {
        int idx = (int)display;
        if (idx >= Display.displays.Length)
        {
            Debug.LogWarning($"Target display {(idx + 1)} not available. Falling back to Display 1.");
            idx = 0;
        }
        return idx;
    }

    [ContextMenu("Apply Now")]
    private void ApplyNowContext()
    {
        if (activateDisplaysOnStart)
        {
            ActivateAllAvailableDisplays();
        }
        int targetIndex = GetSafeDisplayIndex();
        if (retargetAllCameras)
        {
            SetCamerasTargetDisplay(targetIndex);
        }
        ApplyResolutionForDisplay(targetIndex);
    }

    void OnValidate()
    {
        // In edit mode, keep camera targetDisplay in sync for preview convenience
        if (!Application.isPlaying && retargetAllCameras)
        {
            SetCamerasTargetDisplay((int)display);
        }
    }
}
