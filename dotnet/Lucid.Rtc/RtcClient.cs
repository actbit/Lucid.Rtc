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
        string? configJson = null;
        if (config != null)
        {
            configJson = JsonSerializer.Serialize(new
            {
                stun_servers = config.StunServers,
                turn_server_url = config.TurnServerUrl,
                turn_username = config.TurnUsername,
                turn_password = config.TurnPassword,
                ice_connection_timeout_ms = config.IceConnectionTimeoutMs,
                data_channel_reliable = config.DataChannelReliable
            });
        }

        _handle = NativeMethods.webrtc_sharp_create_client(configJson);

        if (_handle != IntPtr.Zero)
        {
            _running = true;
            _pollThread = new Thread(PollLoop)
            {
                IsBackground = true,
                Name = "RtcClient Poll Thread"
            };
            _pollThread.Start();
        }
    }

    private void PollLoop()
    {
        while (_running)
        {
            try
            {
                var eventsPtr = NativeMethods.webrtc_sharp_poll_events(_handle);
                if (eventsPtr != IntPtr.Zero)
                {
                    var json = Marshal.PtrToStringUTF8(eventsPtr);
                    NativeMethods.webrtc_sharp_free_string(eventsPtr);

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
            catch (Exception)
            {
                // Ignore exceptions in poll thread
            }

            Thread.Sleep(_pollIntervalMs);
        }
    }

    /// <summary>
    /// Get the library version.
    /// </summary>
    public static string GetVersion()
    {
        var ptr = NativeMethods.webrtc_sharp_version();
        return Marshal.PtrToStringUTF8(ptr) ?? "unknown";
    }

    /// <summary>
    /// Create an offer for a new peer connection.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <returns>The SDP offer, or null on failure.</returns>
    public string? CreateOffer(string peerId)
    {
        ThrowIfDisposed();

        var ptr = NativeMethods.webrtc_sharp_create_offer(_handle, peerId);
        if (ptr == IntPtr.Zero)
            return null;

        var sdp = Marshal.PtrToStringUTF8(ptr);
        NativeMethods.webrtc_sharp_free_string(ptr);
        return sdp;
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

        var ptr = NativeMethods.webrtc_sharp_set_remote_offer(_handle, peerId, sdp);
        if (ptr == IntPtr.Zero)
            return null;

        var answer = Marshal.PtrToStringUTF8(ptr);
        NativeMethods.webrtc_sharp_free_string(ptr);
        return answer;
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
        return NativeMethods.webrtc_sharp_set_remote_answer(_handle, peerId, sdp) == 0;
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
        return NativeMethods.webrtc_sharp_add_ice_candidate(_handle, peerId, candidate, sdpMid, sdpMlineIndex) == 0;
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
        return NativeMethods.webrtc_sharp_send_message(_handle, peerId, data, (nuint)data.Length) == 0;
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
        return NativeMethods.webrtc_sharp_is_connected(_handle, peerId) == 1;
    }

    /// <summary>
    /// Wait for ICE connection to be established.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <returns>True if connected within timeout.</returns>
    public bool WaitForIceConnected(string peerId)
    {
        ThrowIfDisposed();
        return NativeMethods.webrtc_sharp_wait_for_ice_connected(_handle, peerId) == 0;
    }

    /// <summary>
    /// Close a peer connection.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <returns>True if successful.</returns>
    public bool ClosePeer(string peerId)
    {
        ThrowIfDisposed();
        return NativeMethods.webrtc_sharp_close_peer(_handle, peerId) == 0;
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
        if (_handle != IntPtr.Zero)
        {
            _running = false;
            _pollThread?.Join(1000);

            NativeMethods.webrtc_sharp_destroy_client(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
