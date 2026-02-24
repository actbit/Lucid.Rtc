namespace Lucid.Rtc;

/// <summary>
/// Configuration for RTC client.
/// </summary>
public sealed class RtcConfig
{
    /// <summary>
    /// STUN server URLs.
    /// </summary>
    public string[] StunServers { get; init; } = new[] { "stun:stun.l.google.com:19302" };

    /// <summary>
    /// TURN server URL.
    /// </summary>
    public string? TurnServerUrl { get; init; }

    /// <summary>
    /// TURN username.
    /// </summary>
    public string? TurnUsername { get; init; }

    /// <summary>
    /// TURN password.
    /// </summary>
    public string? TurnPassword { get; init; }

    /// <summary>
    /// ICE connection timeout in milliseconds.
    /// </summary>
    public int IceConnectionTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Whether data channel is reliable.
    /// </summary>
    public bool DataChannelReliable { get; init; } = true;
}
