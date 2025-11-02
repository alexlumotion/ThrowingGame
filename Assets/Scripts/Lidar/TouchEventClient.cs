using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.Events;

public class TouchEventClient : MonoBehaviour
{
    [Header("TCP connection")]
    public string host = "127.0.0.1";
    public int port = 9100;
    [Tooltip("Max time to wait for a TCP connection before retrying (seconds).")]
    public float connectTimeoutSeconds = 5f;
    [Tooltip("Delay before the next connection attempt (seconds).")]
    public float reconnectDelaySeconds = 1f;
    [Tooltip("Optional read timeout in seconds (0 disables).")]
    public float receiveTimeoutSeconds = 0f;

    [Serializable]
    public class TouchEventMessageUnityEvent : UnityEvent<string> { }

    /// <summary>Main-thread event raised for every received raw JSON line.</summary>
    public event Action<string> TouchEventReceived;
    [Tooltip("Invoked on the main thread for each received JSON payload.")]
    public TouchEventMessageUnityEvent onTouchEventReceived = new TouchEventMessageUnityEvent();

    private Thread listenerThread;
    private volatile bool running;
    private readonly ConcurrentQueue<string> pendingMessages = new ConcurrentQueue<string>();

    void Start()
    {
        running = true;
        listenerThread = new Thread(ListenLoop) { IsBackground = true };
        listenerThread.Start();
    }

    private void ListenLoop()
    {
        while (running)
        {
            try
            {
                ListenForEvents();
            }
            catch (TimeoutException tex)
            {
                Debug.LogWarning($"[TouchClient] Connection timed out: {tex.Message}");
            }
            catch (SocketException sex)
            {
                Debug.LogWarning($"[TouchClient] Socket error ({sex.SocketErrorCode}): {sex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TouchClient] Connection lost: {ex.Message}");
            }

            if (running)
            {
                int sleepMs = Mathf.Max(0, (int)(reconnectDelaySeconds * 1000f));
                Thread.Sleep(sleepMs > 0 ? sleepMs : 1000);
            }
        }
    }

    void OnDestroy()
    {
        running = false;
        listenerThread?.Join(500);
    }

    void Update()
    {
        while (pendingMessages.TryDequeue(out var message))
        {
            TouchEventReceived?.Invoke(message);
            onTouchEventReceived?.Invoke(message);
        }
    }

    private void ListenForEvents()
    {
        var endpoint = ResolveEndpoint();
        using (Socket socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
        {
            socket.NoDelay = true;

            int receiveTimeoutMs = Mathf.RoundToInt(receiveTimeoutSeconds * 1000f);
            if (receiveTimeoutMs > 0)
            {
                socket.ReceiveTimeout = receiveTimeoutMs;
            }

            float timeoutSeconds = Mathf.Max(0.1f, connectTimeoutSeconds);
            Debug.Log($"[TouchClient] Connecting to {endpoint.Address}:{endpoint.Port} with timeout {timeoutSeconds:0.0}s …");

            IAsyncResult result = socket.BeginConnect(endpoint, null, null);
            bool connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(timeoutSeconds));
            if (!connected)
            {
                socket.Close();
                throw new TimeoutException($"No response from {endpoint.Address}:{endpoint.Port} after {timeoutSeconds:0.0}s");
            }

            try
            {
                socket.EndConnect(result);
            }
            catch (SocketException)
            {
                socket.Close();
                throw;
            }

            Debug.Log("[TouchClient] Connected (socket)");

            using (NetworkStream networkStream = new NetworkStream(socket, ownsSocket: true))
            using (var reader = new StreamReader(networkStream))
            {
                if (receiveTimeoutMs > 0)
                {
                    networkStream.ReadTimeout = receiveTimeoutMs;
                }

                string line;
                while (running && (line = reader.ReadLine()) != null)
                {
                    Debug.Log($"[TouchClient] Event: {line}");
                    pendingMessages.Enqueue(line);
                }
            }
        }
    }

    private IPEndPoint ResolveEndpoint()
    {
        if (IPAddress.TryParse(host, out var ip))
        {
            return new IPEndPoint(ip, port);
        }

        IPAddress[] candidates = Dns.GetHostAddresses(host);
        foreach (var candidate in candidates)
        {
            if (candidate.AddressFamily == AddressFamily.InterNetwork)
            {
                return new IPEndPoint(candidate, port);
            }
        }

        throw new InvalidOperationException($"Unable to resolve IPv4 address for host '{host}'");
    }
}
