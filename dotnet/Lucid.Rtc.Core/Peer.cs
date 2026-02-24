using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Lucid.Rtc;

/// <summary>
/// Represents a peer connection state.
/// </summary>
public enum PeerState
{
    /// <summary>
    /// Connection is being established.
    /// </summary>
    Connecting,

    /// <summary>
    /// Connection is established and data can be sent.
    /// </summary>
    Connected,

    /// <summary>
    /// Connection is disconnected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Connection failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Connection is closed.
    /// </summary>
    Closed
}

/// <summary>
/// Represents a peer connection.
/// </summary>
public sealed class Peer
{
    private readonly RtcConnection _connection;
    private readonly IntPtr _nativeHandle;
    private string? _videoTrackId;
    private string? _audioTrackId;
    private bool _videoEnabled;
    private bool _audioEnabled;

    /// <summary>
    /// Gets the peer identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets whether the peer is connected.
    /// </summary>
    public bool IsConnected { get; internal set; }

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public PeerState State { get; internal set; }

    /// <summary>
    /// Gets whether video is enabled for this peer.
    /// </summary>
    public bool IsVideoEnabled => _videoEnabled;

    /// <summary>
    /// Gets whether audio is enabled for this peer.
    /// </summary>
    public bool IsAudioEnabled => _audioEnabled;

    internal Peer(RtcConnection connection, string peerId)
    {
        _connection = connection;
        _nativeHandle = connection.NativeHandle;
        Id = peerId;
        State = PeerState.Connecting;
    }

    /// <summary>
    /// Enable video with the specified codec.
    /// </summary>
    /// <param name="codec">Video codec: "vp8", "vp9", "h264", "av1".</param>
    /// <returns>This peer for chaining.</returns>
    public Peer WithVideo(string codec = "vp8")
    {
        if (_videoEnabled)
            return this;

        var trackId = CreateMediaTrack("video", codec);
        if (!string.IsNullOrEmpty(trackId))
        {
            _videoTrackId = trackId;
            _videoEnabled = true;
        }
        return this;
    }

    /// <summary>
    /// Enable audio with the specified codec.
    /// </summary>
    /// <param name="codec">Audio codec: "opus", "pcmu", "pcma".</param>
    /// <returns>This peer for chaining.</returns>
    public Peer WithAudio(string codec = "opus")
    {
        if (_audioEnabled)
            return this;

        var trackId = CreateMediaTrack("audio", codec);
        if (!string.IsNullOrEmpty(trackId))
        {
            _audioTrackId = trackId;
            _audioEnabled = true;
        }
        return this;
    }

    private string? CreateMediaTrack(string kind, string codec)
    {
        var config = new { kind, codec };
        var configJson = JsonSerializer.Serialize(config);
        var configPtr = Marshal.StringToCoTaskMemUTF8(configJson);

        try
        {
            var ptr = Native.NativeMethods.lucid_rtc_create_media_track(_nativeHandle, configPtr);
            if (ptr == IntPtr.Zero)
                return null;

            var trackId = Marshal.PtrToStringUTF8(ptr);
            Native.NativeMethods.lucid_rtc_free_string(ptr);
            return trackId;
        }
        finally
        {
            Marshal.FreeCoTaskMem(configPtr);
        }
    }

    private bool AddTrackToPeer(string trackId)
    {
        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(Id);
        var trackIdPtr = Marshal.StringToCoTaskMemUTF8(trackId);

        try
        {
            return Native.NativeMethods.lucid_rtc_add_track_to_peer(_nativeHandle, peerIdPtr, trackIdPtr) == 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
            Marshal.FreeCoTaskMem(trackIdPtr);
        }
    }

    /// <summary>
    /// Set the remote SDP offer.
    /// </summary>
    /// <param name="sdp">The SDP offer.</param>
    /// <returns>This peer for chaining.</returns>
    public Peer SetRemoteOffer(string sdp)
    {
        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(Id);
        var sdpPtr = Marshal.StringToCoTaskMemUTF8(sdp);

        try
        {
            var ptr = Native.NativeMethods.lucid_rtc_set_remote_offer(_nativeHandle, peerIdPtr, sdpPtr);
            if (ptr == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to set remote offer for peer {Id}");

            // Add media tracks if enabled
            if (_videoEnabled && _videoTrackId != null)
                AddTrackToPeer(_videoTrackId);
            if (_audioEnabled && _audioTrackId != null)
                AddTrackToPeer(_audioTrackId);

            Native.NativeMethods.lucid_rtc_free_string(ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
            Marshal.FreeCoTaskMem(sdpPtr);
        }

        return this;
    }

    /// <summary>
    /// Set the remote SDP answer.
    /// </summary>
    /// <param name="sdp">The SDP answer.</param>
    /// <returns>This peer for chaining.</returns>
    public Peer SetRemoteAnswer(string sdp)
    {
        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(Id);
        var sdpPtr = Marshal.StringToCoTaskMemUTF8(sdp);

        try
        {
            var result = Native.NativeMethods.lucid_rtc_set_remote_answer(_nativeHandle, peerIdPtr, sdpPtr);
            if (result != 0)
                throw new InvalidOperationException($"Failed to set remote answer for peer {Id}");
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
            Marshal.FreeCoTaskMem(sdpPtr);
        }

        return this;
    }

    /// <summary>
    /// Add an ICE candidate.
    /// </summary>
    /// <param name="candidate">The ICE candidate.</param>
    /// <returns>This peer for chaining.</returns>
    public Peer AddIceCandidate(IceCandidate candidate)
    {
        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(Id);
        var candidatePtr = Marshal.StringToCoTaskMemUTF8(candidate.Candidate);
        var sdpMidPtr = Marshal.StringToCoTaskMemUTF8(candidate.SdpMid);

        try
        {
            Native.NativeMethods.lucid_rtc_add_ice_candidate(_nativeHandle, peerIdPtr, candidatePtr, sdpMidPtr, candidate.SdpMlineIndex);
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
            Marshal.FreeCoTaskMem(candidatePtr);
            Marshal.FreeCoTaskMem(sdpMidPtr);
        }

        return this;
    }

    /// <summary>
    /// Send a string message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Send(string message)
    {
        Send(Encoding.UTF8.GetBytes(message));
    }

    /// <summary>
    /// Send binary data.
    /// </summary>
    /// <param name="data">The data.</param>
    public void Send(byte[] data)
    {
        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(Id);

        try
        {
            var result = Native.NativeMethods.lucid_rtc_send_message(_nativeHandle, peerIdPtr, data, (nuint)data.Length);
            if (result != 0)
                throw new InvalidOperationException($"Failed to send message to peer {Id}");
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
        }
    }

    /// <summary>
    /// Send video RTP data.
    /// </summary>
    /// <param name="rtpData">The RTP packet data.</param>
    public void SendVideo(byte[] rtpData)
    {
        if (!_videoEnabled || string.IsNullOrEmpty(_videoTrackId))
            throw new InvalidOperationException("Video is not enabled for this peer");

        var trackIdPtr = Marshal.StringToCoTaskMemUTF8(_videoTrackId);

        try
        {
            var result = Native.NativeMethods.lucid_rtc_send_media_data(_nativeHandle, trackIdPtr, rtpData, (nuint)rtpData.Length);
            if (result != 0)
                throw new InvalidOperationException("Failed to send video data");
        }
        finally
        {
            Marshal.FreeCoTaskMem(trackIdPtr);
        }
    }

    /// <summary>
    /// Send audio RTP data.
    /// </summary>
    /// <param name="rtpData">The RTP packet data.</param>
    public void SendAudio(byte[] rtpData)
    {
        if (!_audioEnabled || string.IsNullOrEmpty(_audioTrackId))
            throw new InvalidOperationException("Audio is not enabled for this peer");

        var trackIdPtr = Marshal.StringToCoTaskMemUTF8(_audioTrackId);

        try
        {
            var result = Native.NativeMethods.lucid_rtc_send_media_data(_nativeHandle, trackIdPtr, rtpData, (nuint)rtpData.Length);
            if (result != 0)
                throw new InvalidOperationException("Failed to send audio data");
        }
        finally
        {
            Marshal.FreeCoTaskMem(trackIdPtr);
        }
    }

    /// <summary>
    /// Close the peer connection.
    /// </summary>
    public async Task CloseAsync()
    {
        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(Id);

        try
        {
            Native.NativeMethods.lucid_rtc_close_peer(_nativeHandle, peerIdPtr);
            State = PeerState.Closed;
            IsConnected = false;
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
        }
    }
}
