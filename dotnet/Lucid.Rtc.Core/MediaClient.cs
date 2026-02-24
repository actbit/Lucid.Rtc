using System.Runtime.InteropServices;
using System.Text.Json;
using Lucid.Rtc.Native;

namespace Lucid.Rtc;

/// <summary>
/// Media codec capability information.
/// </summary>
public sealed class MediaCodec
{
    /// <summary>
    /// MIME type (e.g., "audio/opus", "video/VP8").
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// Clock rate in Hz.
    /// </summary>
    public required uint ClockRate { get; init; }

    /// <summary>
    /// Number of audio channels (for audio codecs).
    /// </summary>
    public ushort Channels { get; init; }

    /// <summary>
    /// SDP format parameters.
    /// </summary>
    public string? SdpFmtpLine { get; init; }
}

/// <summary>
/// Configuration for creating a media track.
/// </summary>
public sealed class MediaTrackConfig
{
    /// <summary>
    /// Track kind: "audio" or "video".
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Codec name: "opus", "vp8", "vp9", "h264", "av1".
    /// </summary>
    public required string Codec { get; init; }

    /// <summary>
    /// Custom track ID (optional).
    /// </summary>
    public string? TrackId { get; init; }

    /// <summary>
    /// Custom stream ID (optional).
    /// </summary>
    public string? StreamId { get; init; }
}

/// <summary>
/// Extended client with media support (Pion backend only).
/// </summary>
public sealed class MediaClient : RtcClient
{
    private static bool? _isPionBackend;

    /// <summary>
    /// Check if the current backend is Pion (required for media features).
    /// </summary>
    public static bool IsPionBackend
    {
        get
        {
            if (_isPionBackend == null)
            {
                try
                {
                    var ptr = NativeMethods.lucid_rtc_get_backend();
                    var backend = Marshal.PtrToStringUTF8(ptr);
                    _isPionBackend = backend == "pion";
                }
                catch
                {
                    _isPionBackend = false;
                }
            }
            return _isPionBackend.Value;
        }
    }

    /// <summary>
    /// Create a new media client with the specified configuration.
    /// </summary>
    public MediaClient(RtcConfig? config = null) : base(config)
    {
        if (!IsPionBackend)
        {
            System.Diagnostics.Debug.WriteLine("[Lucid.Rtc] Warning: Media features require Pion backend");
        }
    }

    /// <summary>
    /// Get list of supported media codecs.
    /// </summary>
    /// <returns>Array of supported codecs.</returns>
    public static MediaCodec[] GetSupportedCodecs()
    {
        try
        {
            var ptr = NativeMethods.lucid_rtc_get_supported_codecs();
            if (ptr == IntPtr.Zero)
                return Array.Empty<MediaCodec>();

            var json = Marshal.PtrToStringUTF8(ptr);
            NativeMethods.lucid_rtc_free_string(ptr);

            if (string.IsNullOrEmpty(json))
                return Array.Empty<MediaCodec>();

            return JsonSerializer.Deserialize<MediaCodec[]>(json) ?? Array.Empty<MediaCodec>();
        }
        catch
        {
            return Array.Empty<MediaCodec>();
        }
    }

    /// <summary>
    /// Create a media track for sending audio/video.
    /// </summary>
    /// <param name="config">Track configuration.</param>
    /// <returns>Track ID, or null on failure.</returns>
    public string? CreateMediaTrack(MediaTrackConfig config)
    {
        var configObj = new
        {
            kind = config.Kind,
            codec = config.Codec,
            track_id = config.TrackId,
            stream_id = config.StreamId
        };
        var configJson = JsonSerializer.Serialize(configObj);
        var configPtr = Marshal.StringToCoTaskMemUTF8(configJson);

        try
        {
            var ptr = NativeMethods.lucid_rtc_create_media_track(NativeHandle, configPtr);
            if (ptr == IntPtr.Zero)
                return null;

            var trackId = Marshal.PtrToStringUTF8(ptr);
            NativeMethods.lucid_rtc_free_string(ptr);
            return trackId;
        }
        finally
        {
            Marshal.FreeCoTaskMem(configPtr);
        }
    }

    /// <summary>
    /// Add a media track to a peer connection.
    /// </summary>
    /// <param name="peerId">Peer ID.</param>
    /// <param name="trackId">Track ID.</param>
    /// <returns>True if successful.</returns>
    public bool AddTrackToPeer(string peerId, string trackId)
    {
        var peerIdPtr = Marshal.StringToCoTaskMemUTF8(peerId);
        var trackIdPtr = Marshal.StringToCoTaskMemUTF8(trackId);

        try
        {
            return NativeMethods.lucid_rtc_add_track_to_peer(NativeHandle, peerIdPtr, trackIdPtr) == 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(peerIdPtr);
            Marshal.FreeCoTaskMem(trackIdPtr);
        }
    }

    /// <summary>
    /// Send RTP media data to a track.
    /// </summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="rtpData">RTP packet data.</param>
    /// <returns>True if successful.</returns>
    public bool SendMediaData(string trackId, byte[] rtpData)
    {
        var trackIdPtr = Marshal.StringToCoTaskMemUTF8(trackId);

        try
        {
            return NativeMethods.lucid_rtc_send_media_data(NativeHandle, trackIdPtr, rtpData, (nuint)rtpData.Length) == 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(trackIdPtr);
        }
    }

    /// <summary>
    /// Remove a media track.
    /// </summary>
    /// <param name="trackId">Track ID.</param>
    /// <returns>True if successful.</returns>
    public bool RemoveMediaTrack(string trackId)
    {
        var trackIdPtr = Marshal.StringToCoTaskMemUTF8(trackId);

        try
        {
            return NativeMethods.lucid_rtc_remove_media_track(NativeHandle, trackIdPtr) == 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(trackIdPtr);
        }
    }
}
