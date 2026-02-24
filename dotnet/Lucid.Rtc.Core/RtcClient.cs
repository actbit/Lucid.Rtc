using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Lucid.Rtc.Native;

namespace Lucid.Rtc;

/// <summary>
/// WebRTC client for peer-to-peer communication.
/// </summary>
public sealed class RtcClient : IDisposable
{
    private IntPtr _handle;
    private Thread? _pollThread;
    private volatile bool _running;
    private readonly ConcurrentQueue<RtcEvent> _eventQueue = new();
    private readonly int _pollIntervalMs = 10;
    private readonly object _disposeLock = new();
    private bool _disposed;

    /// <summary>
    /// Event raised when a WebRTC event is received.
    /// </summary>
    public event EventHandler<RtcEvent>? EventReceived;

    /// <summary>
    /// Gets whether the native client is available.
    /// </summary>
    public bool IsNativeAvailable => _handle != IntPtr.Zero;

    /// <summary>
    /// Create a new RTC client with the specified configuration.
    /// </summary>
    /// <param name="config">The RTC configuration.</param>
    public RtcClient(RtcConfig? config = null)
    {
        IntPtr configPtr = IntPtr.Zero;
        try
        {
            if (config != null)
            {
                // Build config JSON matching Rust Config struct format
                var configObj = new
                {
                    stun_servers = config.StunServers,
                    turn_server = config.TurnServerUrl != null ? new
                    {
                        url = config.TurnServerUrl,
                        username = config.TurnUsername ?? "",
                        password = config.TurnPassword ?? ""
                    } : null,
                    ice_timeout_ms = (ulong)config.IceConnectionTimeoutMs,
                    data_channel_reliable = config.DataChannelReliable
                };
                var configJson = JsonSerializer.Serialize(configObj);
                configPtr = Marshal.StringToCoTaskMemUTF8(configJson);
            }

            _handle = NativeMethods.lucid_rtc_create_client(configPtr);

            if (_handle != IntPtr.Zero)
            {
                _running = true;
                _pollThread = new Thread(PollLoop)
                {
                    IsBackground = true,
                    Name = "Lucid.Rtc Poll Thread"
                };
                _pollThread.Start();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[Lucid.Rtc] Native library not available or initialization failed");
            }
        }
        finally
        {
            if (configPtr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(configPtr);
        }
    }

    private void PollLoop()
    {
        while (_running)
        {
            try
            {
                var eventsPtr = NativeMethods.lucid_rtc_poll_events(_handle);
                if (eventsPtr != IntPtr.Zero)
                {
                    var json = Marshal.PtrToStringUTF8(eventsPtr);
                    NativeMethods.lucid_rtc_free_string(eventsPtr);

                    if (!string.IsNullOrEmpty(json))
                    {
                        var events = RtcEventConverter.ParseEvents(json);
                        foreach (var evt in events)
                        {
                            _eventQueue.Enqueue(evt);
                            EventReceived?.Invoke(this, evt);
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected during shutdown, exit gracefully
                break;
            }
            catch (Exception ex)
            {
                // Log error but continue polling
                System.Diagnostics.Debug.WriteLine($"[Lucid.Rtc] Poll error: {ex}");
            }

            Thread.Sleep(_pollIntervalMs);
        }
    }

    /// <summary>
    /// Get the library version.
    /// </summary>
    public static string GetVersion()
    {
        try
        {
            var ptr = NativeMethods.lucid_rtc_version();
            return Marshal.PtrToStringUTF8(ptr) ?? "unknown";
        }
        catch
        {
            return "native-unavailable";
        }
    }

    /// <summary>
    /// Create an offer for a new peer connection.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <returns>The SDP offer, or null on failure.</returns>
    public string? CreateOffer(string peerId)
    {
        ThrowIfDisposed();

        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(peerId);
        try
        {
            var ptr = NativeMethods.lucid_rtc_create_offer(_handle, peerIdPtr);
            if (ptr == IntPtr.Zero)
                return null;

            var sdp = Marshal.PtrToStringUTF8(ptr);
            NativeMethods.lucid_rtc_free_string(ptr);
            return sdp;
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
        }
    }

    /// <summary>
    /// Set remote offer and create an answer.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <param name="sdp">The remote SDP offer.</param>
    /// <returns>The SDP answer, or null on failure.</returns>
    public string? SetRemoteOffer(string peerId, string sdp)
    {
        ThrowIfDisposed();

        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(peerId);
        var sdpPtr = Marshal.StringToCoTaskMemUTF8(sdp);
        try
        {
            var ptr = NativeMethods.lucid_rtc_set_remote_offer(_handle, peerIdPtr, sdpPtr);
            if (ptr == IntPtr.Zero)
                return null;

            var answer = Marshal.PtrToStringUTF8(ptr);
            NativeMethods.lucid_rtc_free_string(ptr);
            return answer;
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
            Marshal.FreeCoTaskMem(sdpPtr);
        }
    }

    /// <summary>
    /// Set remote answer (for offerer).
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <param name="sdp">The remote SDP answer.</param>
    /// <returns>True if successful.</returns>
    public bool SetRemoteAnswer(string peerId, string sdp)
    {
        ThrowIfDisposed();

        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(peerId);
        var sdpPtr = Marshal.StringToCoTaskMemUTF8(sdp);
        try
        {
            return NativeMethods.lucid_rtc_set_remote_answer(_handle, peerIdPtr, sdpPtr) == 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
            Marshal.FreeCoTaskMem(sdpPtr);
        }
    }

    /// <summary>
    /// Add an ICE candidate.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <param name="candidate">The candidate string.</param>
    /// <param name="sdpMid">The SDP media identifier.</param>
    /// <param name="sdpMlineIndex">The SDP media line index.</param>
    /// <returns>True if successful.</returns>
    public bool AddIceCandidate(string peerId, string candidate, string sdpMid, int sdpMlineIndex)
    {
        ThrowIfDisposed();

        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(peerId);
        var candidatePtr = Marshal.StringToCoTaskMemUTF8(candidate);
        var sdpMidPtr = Marshal.StringToCoTaskMemUTF8(sdpMid);
        try
        {
            return NativeMethods.lucid_rtc_add_ice_candidate(_handle, peerIdPtr, candidatePtr, sdpMidPtr, sdpMlineIndex) == 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
            Marshal.FreeCoTaskMem(candidatePtr);
            Marshal.FreeCoTaskMem(sdpMidPtr);
        }
    }

    /// <summary>
    /// Add an ICE candidate from an IceCandidate object.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <param name="iceCandidate">The ICE candidate.</param>
    /// <returns>True if successful.</returns>
    public bool AddIceCandidate(string peerId, IceCandidate iceCandidate)
    {
        return AddIceCandidate(peerId, iceCandidate.Candidate, iceCandidate.SdpMid, iceCandidate.SdpMlineIndex);
    }

    /// <summary>
    /// Send a message to a peer.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <param name="data">The data to send.</param>
    /// <returns>True if successful.</returns>
    public bool SendMessage(string peerId, byte[] data)
    {
        ThrowIfDisposed();

        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(peerId);
        try
        {
            return NativeMethods.lucid_rtc_send_message(_handle, peerIdPtr, data, (nuint)data.Length) == 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
        }
    }

    /// <summary>
    /// Send a string message to a peer.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <param name="message">The message to send.</param>
    /// <returns>True if successful.</returns>
    public bool SendMessage(string peerId, string message)
    {
        return SendMessage(peerId, Encoding.UTF8.GetBytes(message));
    }

    /// <summary>
    /// Check if a peer is connected.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <returns>True if connected.</returns>
    public bool IsConnected(string peerId)
    {
        ThrowIfDisposed();

        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(peerId);
        try
        {
            return NativeMethods.lucid_rtc_is_connected(_handle, peerIdPtr) == 1;
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
        }
    }

    /// <summary>
    /// Wait for ICE connection to be established.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <returns>True if connected within timeout.</returns>
    public bool WaitForIceConnected(string peerId)
    {
        ThrowIfDisposed();

        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(peerId);
        try
        {
            return NativeMethods.lucid_rtc_wait_for_ice_connected(_handle, peerIdPtr) == 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
        }
    }

    /// <summary>
    /// Close a peer connection.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <returns>True if successful.</returns>
    public bool ClosePeer(string peerId)
    {
        ThrowIfDisposed();

        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(peerId);
        try
        {
            return NativeMethods.lucid_rtc_close_peer(_handle, peerIdPtr) == 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
        }
    }

    /// <summary>
    /// Broadcast a message to all connected peers.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <returns>True if successful.</returns>
    public bool Broadcast(byte[] data)
    {
        ThrowIfDisposed();
        return NativeMethods.lucid_rtc_broadcast(_handle, data, (nuint)data.Length) == 0;
    }

    /// <summary>
    /// Broadcast a string message to all connected peers.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <returns>True if successful.</returns>
    public bool Broadcast(string message)
    {
        return Broadcast(Encoding.UTF8.GetBytes(message));
    }

    /// <summary>
    /// Close all peer connections.
    /// </summary>
    /// <returns>True if successful.</returns>
    public bool CloseAllPeers()
    {
        ThrowIfDisposed();
        return NativeMethods.lucid_rtc_close_all(_handle) == 0;
    }

    /// <summary>
    /// Try to get the next event from the queue.
    /// </summary>
    /// <param name="evt">The event.</param>
    /// <returns>True if an event was available.</returns>
    public bool TryGetEvent([NotNullWhen(true)] out RtcEvent? evt)
    {
        return _eventQueue.TryDequeue(out evt);
    }

    private void ThrowIfDisposed()
    {
        if (_handle == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(RtcClient));
    }

    /// <summary>
    /// Dispose the client.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        lock (_disposeLock)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        if (_handle != IntPtr.Zero)
        {
            _running = false;

            // Wait for poll thread with longer timeout
            if (_pollThread != null && !_pollThread.Join(5000))
            {
                System.Diagnostics.Debug.WriteLine("[Lucid.Rtc] Warning: Poll thread did not exit gracefully");
            }

            NativeMethods.lucid_rtc_destroy_client(_handle);
            _handle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Finalizer to ensure native resources are released.
    /// </summary>
    ~RtcClient()
    {
        Dispose(false);
    }
}
