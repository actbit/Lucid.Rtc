using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Lucid.Rtc;

/// <summary>
/// WebRTC connection with fluent API.
/// </summary>
public sealed class RtcConnection : IAsyncDisposable
{
    private readonly RtcConnectionConfig _config;
    private IntPtr _handle;
    private readonly Thread? _pollThread;
    private volatile bool _running;
    private bool _disposed;

    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly ConcurrentDictionary<string, Peer> _peers = new();

    /// <summary>
    /// Gets the native handle.
    /// </summary>
    internal IntPtr NativeHandle => _handle;

    /// <summary>
    /// Gets whether the connection is available.
    /// </summary>
    public bool IsConnected => _handle != IntPtr.Zero;

    /// <summary>
    /// Gets the library version.
    /// </summary>
    public static string Version
    {
        get
        {
            try
            {
                var ptr = Native.NativeMethods.lucid_rtc_version();
                return Marshal.PtrToStringUTF8(ptr) ?? "unknown";
            }
            catch
            {
                return "native-unavailable";
            }
        }
    }

    internal RtcConnection(RtcConnectionConfig config)
    {
        _config = config;

        var configJson = BuildConfigJson();
        var configPtr = Marshal.StringToCoTaskMemUTF8(configJson);

        try
        {
            _handle = Native.NativeMethods.lucid_rtc_create_client(configPtr);

            if (_handle != IntPtr.Zero)
            {
                _running = true;
                _pollThread = new Thread(PollLoop)
                {
                    IsBackground = true,
                    Name = "RtcConnection Poll Thread"
                };
                _pollThread.Start();
            }
            else
            {
                throw new InvalidOperationException("Failed to create native RTC client");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(configPtr);
        }
    }

    private string BuildConfigJson()
    {
        var configObj = new
        {
            stun_servers = _config.StunServers,
            turn_server = _config.TurnServerUrl != null ? new
            {
                url = _config.TurnServerUrl,
                username = _config.TurnUsername ?? "",
                password = _config.TurnPassword ?? ""
            } : null,
            ice_timeout_ms = (ulong)_config.IceConnectionTimeoutMs,
            data_channel_reliable = _config.DataChannelReliable
        };
        return JsonSerializer.Serialize(configObj);
    }

    private void PollLoop()
    {
        while (_running)
        {
            try
            {
                var eventsPtr = Native.NativeMethods.lucid_rtc_poll_events(_handle);
                if (eventsPtr != IntPtr.Zero)
                {
                    var json = Marshal.PtrToStringUTF8(eventsPtr);
                    Native.NativeMethods.lucid_rtc_free_string(eventsPtr);

                    if (!string.IsNullOrEmpty(json))
                    {
                        ProcessEvents(json);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RtcConnection] Poll error: {ex}");
            }

            Thread.Sleep(10);
        }
    }

    private void ProcessEvents(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var evt = ConvertToTypedEvent(element);
                if (evt != null)
                {
                    InvokeHandlers(evt);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RtcConnection] Event parse error: {ex}");
        }
    }

    private RtcEventBase? ConvertToTypedEvent(JsonElement element)
    {
        if (!element.TryGetProperty("type", out var typeProp))
            return null;

        var type = typeProp.GetString() ?? "";
        var peerId = element.TryGetProperty("peer_id", out var peerIdProp) ? peerIdProp.GetString() : null;

        return type switch
        {
            "peer_connected" => new PeerConnectedEvent
            {
                PeerId = peerId,
                Peer = peerId != null ? GetOrCreatePeer(peerId) : null
            },
            "peer_disconnected" => HandlePeerDisconnected(peerId),
            "message_received" => new MessageReceivedEvent
            {
                PeerId = peerId,
                Peer = peerId != null ? GetPeer(peerId) : null,
                Data = Convert.FromBase64String(element.GetProperty("data").GetString()!)
            },
            "ice_candidate" => new IceCandidateEvent
            {
                PeerId = peerId,
                Peer = peerId != null ? GetPeer(peerId) : null,
                Candidate = ParseIceCandidate(element.GetProperty("candidate"))
            },
            "offer_ready" => new OfferReadyEvent
            {
                PeerId = peerId,
                Peer = peerId != null ? GetPeer(peerId) : null,
                Sdp = element.GetProperty("sdp").GetString()!
            },
            "answer_ready" => new AnswerReadyEvent
            {
                PeerId = peerId,
                Peer = peerId != null ? GetPeer(peerId) : null,
                Sdp = element.GetProperty("sdp").GetString()!
            },
            "ice_connection_state_change" => HandleIceStateChange(peerId, element.GetProperty("state").GetString()!),
            "data_channel_open" => HandleDataChannelOpen(peerId),
            "data_channel_closed" => new DataChannelClosedEvent
            {
                PeerId = peerId,
                Peer = peerId != null ? GetPeer(peerId) : null
            },
            "video_frame" => new VideoFrameEvent
            {
                PeerId = peerId,
                Peer = peerId != null ? GetPeer(peerId) : null,
                Data = Convert.FromBase64String(element.GetProperty("data").GetString()!)
            },
            "audio_frame" => new AudioFrameEvent
            {
                PeerId = peerId,
                Peer = peerId != null ? GetPeer(peerId) : null,
                Data = Convert.FromBase64String(element.GetProperty("data").GetString()!)
            },
            "error" => new ErrorEvent
            {
                PeerId = peerId,
                Peer = peerId != null ? GetPeer(peerId) : null,
                Message = element.GetProperty("message").GetString()!
            },
            _ => null
        };
    }

    private Peer GetOrCreatePeer(string peerId)
    {
        return _peers.GetOrAdd(peerId, id => new Peer(this, id));
    }

    private RtcEventBase HandlePeerDisconnected(string? peerId)
    {
        if (peerId != null && _peers.TryGetValue(peerId, out var peer))
        {
            peer.IsConnected = false;
            peer.State = PeerState.Disconnected;
        }
        return new PeerDisconnectedEvent { PeerId = peerId, Peer = peerId != null ? GetPeer(peerId) : null };
    }

    private RtcEventBase HandleIceStateChange(string? peerId, string state)
    {
        if (peerId != null && _peers.TryGetValue(peerId, out var peer))
        {
            peer.State = state.ToLower() switch
            {
                "connected" => PeerState.Connected,
                "disconnected" => PeerState.Disconnected,
                "failed" => PeerState.Failed,
                "closed" => PeerState.Closed,
                _ => PeerState.Connecting
            };
            peer.IsConnected = peer.State == PeerState.Connected;
        }
        return new IceConnectionStateChangeEvent
        {
            PeerId = peerId,
            Peer = peerId != null ? GetPeer(peerId) : null,
            State = state
        };
    }

    private RtcEventBase HandleDataChannelOpen(string? peerId)
    {
        if (peerId != null && _peers.TryGetValue(peerId, out var peer))
        {
            peer.IsConnected = true;
            peer.State = PeerState.Connected;
        }
        return new DataChannelOpenEvent { PeerId = peerId, Peer = peerId != null ? GetPeer(peerId) : null };
    }

    private static IceCandidate ParseIceCandidate(JsonElement element)
    {
        return new IceCandidate
        {
            Candidate = element.GetProperty("candidate").GetString()!,
            SdpMid = element.GetProperty("sdp_mid").GetString()!,
            SdpMlineIndex = element.GetProperty("sdp_mline_index").GetInt32()
        };
    }

    private void InvokeHandlers(RtcEventBase evt)
    {
        var eventType = evt.GetType();
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers.ToArray())
            {
                try
                {
                    handler.DynamicInvoke(evt);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RtcConnection] Handler error: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// Register a handler for a specific event type.
    /// </summary>
    /// <typeparam name="T">The event type.</typeparam>
    /// <param name="handler">The handler action.</param>
    /// <returns>This connection for chaining.</returns>
    public RtcConnection On<T>(Action<T> handler) where T : RtcEventBase
    {
        var handlers = _handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
        lock (handlers)
        {
            handlers.Add(handler);
        }
        return this;
    }

    /// <summary>
    /// Remove all handlers for a specific event type.
    /// </summary>
    /// <typeparam name="T">The event type.</typeparam>
    /// <returns>This connection for chaining.</returns>
    public RtcConnection Off<T>() where T : RtcEventBase
    {
        _handlers.TryRemove(typeof(T), out _);
        return this;
    }

    /// <summary>
    /// Remove a specific handler for an event type.
    /// </summary>
    /// <typeparam name="T">The event type.</typeparam>
    /// <param name="handler">The handler to remove.</param>
    /// <returns>This connection for chaining.</returns>
    public RtcConnection Off<T>(Action<T> handler) where T : RtcEventBase
    {
        if (_handlers.TryGetValue(typeof(T), out var handlers))
        {
            lock (handlers)
            {
                handlers.Remove(handler);
            }
        }
        return this;
    }

    /// <summary>
    /// Create a new peer connection.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <returns>The created peer.</returns>
    public async Task<Peer> CreatePeerAsync(string peerId)
    {
        ThrowIfDisposed();

        var peer = GetOrCreatePeer(peerId);

        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(peerId);
        try
        {
            var ptr = Native.NativeMethods.lucid_rtc_create_offer(_handle, peerIdPtr);
            if (ptr == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to create peer {peerId}");

            Native.NativeMethods.lucid_rtc_free_string(ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
        }

        return peer;
    }

    /// <summary>
    /// Get an existing peer by ID.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <returns>The peer, or null if not found.</returns>
    public Peer? GetPeer(string peerId)
    {
        _peers.TryGetValue(peerId, out var peer);
        return peer;
    }

    /// <summary>
    /// Get all connected peers.
    /// </summary>
    public IEnumerable<Peer> GetConnectedPeers()
    {
        return _peers.Values.Where(p => p.IsConnected);
    }

    /// <summary>
    /// Broadcast a message to all connected peers.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Broadcast(string message)
    {
        Broadcast(Encoding.UTF8.GetBytes(message));
    }

    /// <summary>
    /// Broadcast data to all connected peers.
    /// </summary>
    /// <param name="data">The data.</param>
    public void Broadcast(byte[] data)
    {
        ThrowIfDisposed();

        var result = Native.NativeMethods.lucid_rtc_broadcast(_handle, data, (nuint)data.Length);
        if (result != 0)
            throw new InvalidOperationException("Failed to broadcast message");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed || _handle == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(RtcConnection));
    }

    /// <summary>
    /// Dispose the connection asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _running = false;

        if (_pollThread != null && !_pollThread.Join(5000))
        {
            System.Diagnostics.Debug.WriteLine("[RtcConnection] Warning: Poll thread did not exit gracefully");
        }

        if (_handle != IntPtr.Zero)
        {
            Native.NativeMethods.lucid_rtc_destroy_client(_handle);
            _handle = IntPtr.Zero;
        }

        _handlers.Clear();
        _peers.Clear();
    }
}
