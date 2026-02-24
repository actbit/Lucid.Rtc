namespace Lucid.Rtc;

/// <summary>
/// Base class for all RTC events.
/// </summary>
public abstract class RtcEventBase
{
    /// <summary>
    /// The peer ID associated with this event (if applicable).
    /// </summary>
    public string? PeerId { get; init; }

    /// <summary>
    /// The peer associated with this event (if available).
    /// </summary>
    public Peer? Peer { get; internal set; }
}

/// <summary>
/// Event raised when a peer connection is established.
/// </summary>
public sealed class PeerConnectedEvent : RtcEventBase
{
}

/// <summary>
/// Event raised when a peer connection is disconnected.
/// </summary>
public sealed class PeerDisconnectedEvent : RtcEventBase
{
}

/// <summary>
/// Event raised when a message is received from a peer.
/// </summary>
public sealed class MessageReceivedEvent : RtcEventBase
{
    /// <summary>
    /// The raw message data.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// The message data as a UTF-8 string.
    /// </summary>
    public string DataAsString => System.Text.Encoding.UTF8.GetString(Data);
}

/// <summary>
/// Event raised when an ICE candidate is ready.
/// </summary>
public sealed class IceCandidateEvent : RtcEventBase
{
    /// <summary>
    /// The ICE candidate.
    /// </summary>
    public required IceCandidate Candidate { get; init; }
}

/// <summary>
/// Event raised when an SDP offer is ready.
/// </summary>
public sealed class OfferReadyEvent : RtcEventBase
{
    /// <summary>
    /// The SDP offer.
    /// </summary>
    public required string Sdp { get; init; }
}

/// <summary>
/// Event raised when an SDP answer is ready.
/// </summary>
public sealed class AnswerReadyEvent : RtcEventBase
{
    /// <summary>
    /// The SDP answer.
    /// </summary>
    public required string Sdp { get; init; }
}

/// <summary>
/// Event raised when the ICE connection state changes.
/// </summary>
public sealed class IceConnectionStateChangeEvent : RtcEventBase
{
    /// <summary>
    /// The new connection state.
    /// </summary>
    public required string State { get; init; }
}

/// <summary>
/// Event raised when a data channel is opened.
/// </summary>
public sealed class DataChannelOpenEvent : RtcEventBase
{
}

/// <summary>
/// Event raised when a data channel is closed.
/// </summary>
public sealed class DataChannelClosedEvent : RtcEventBase
{
}

/// <summary>
/// Event raised when a video frame is received.
/// </summary>
public sealed class VideoFrameEvent : RtcEventBase
{
    /// <summary>
    /// The video frame data (RTP packet).
    /// </summary>
    public required byte[] Data { get; init; }
}

/// <summary>
/// Event raised when an audio frame is received.
/// </summary>
public sealed class AudioFrameEvent : RtcEventBase
{
    /// <summary>
    /// The audio frame data (RTP packet).
    /// </summary>
    public required byte[] Data { get; init; }
}

/// <summary>
/// Event raised when an error occurs.
/// </summary>
public sealed class ErrorEvent : RtcEventBase
{
    /// <summary>
    /// The error message.
    /// </summary>
    public required string Message { get; init; }
}
