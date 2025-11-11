using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogController : MonoBehaviour
{
    public bool enableLogs = true;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnPrepareCompleted()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnPrepareCompleted");
    }

    public void OnFirstFrameReady()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnFirstFrameReady");
    }

    public void OnStarted()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnStarted");
    }

    public void OnChunkChanged()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnChunkChanged");
    }

    public void OnFrameChanged()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnFrameChanged");
    }

    public void OnNextChunkPreloadStarted()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnNextChunkPreloadStarted");
    }

    public void OnNextChunkPreloadReady()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnNextChunkPreloadReady");
    }

    public void OnNextChunkPreloadFailed()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnNextChunkPreloadFailed");
    }

    public void OnFinished()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnFinished");
    }

    public void OnPlayEvent()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnPlayEvent");
    }

    public void OnPauseEvent()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnPauseEvent");
    }

    public void OnStopEvent()
    {
        if (!enableLogs) return;
        Debug.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!OnStopEvent");
    }

}
