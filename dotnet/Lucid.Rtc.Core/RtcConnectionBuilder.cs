namespace Lucid.Rtc;

/// <summary>
/// Builder for creating <see cref="RtcConnection"/> instances.
/// </summary>
public sealed class RtcConnectionBuilder
{
    private readonly List<string> _stunServers = new();
    private string? _turnServerUrl;
    private string? _turnUsername;
    private string? _turnPassword;
    private int _iceConnectionTimeoutMs = 30000;
    private bool _dataChannelReliable = true;

    /// <summary>
    /// Add a STUN server.
    /// </summary>
    /// <param name="stunServer">The STUN server URL (e.g., "stun:stun.l.google.com:19302").</param>
    /// <returns>This builder.</returns>
    public RtcConnectionBuilder WithStunServer(string stunServer)
    {
        _stunServers.Add(stunServer);
        return this;
    }

    /// <summary>
    /// Add multiple STUN servers.
    /// </summary>
    /// <param name="stunServers">The STUN server URLs.</param>
    /// <returns>This builder.</returns>
    public RtcConnectionBuilder WithStunServers(params string[] stunServers)
    {
        _stunServers.AddRange(stunServers);
        return this;
    }

    /// <summary>
    /// Configure TURN server.
    /// </summary>
    /// <param name="url">The TURN server URL.</param>
    /// <param name="username">The username for authentication.</param>
    /// <param name="password">The password for authentication.</param>
    /// <returns>This builder.</returns>
    public RtcConnectionBuilder WithTurnServer(string url, string username, string password)
    {
        _turnServerUrl = url;
        _turnUsername = username;
        _turnPassword = password;
        return this;
    }

    /// <summary>
    /// Set the ICE connection timeout.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <returns>This builder.</returns>
    public RtcConnectionBuilder WithIceConnectionTimeout(int timeoutMs)
    {
        _iceConnectionTimeoutMs = timeoutMs;
        return this;
    }

    /// <summary>
    /// Set whether the data channel should be reliable (ordered).
    /// </summary>
    /// <param name="reliable">True for reliable, false for unreliable.</param>
    /// <returns>This builder.</returns>
    public RtcConnectionBuilder WithDataChannelReliable(bool reliable)
    {
        _dataChannelReliable = reliable;
        return this;
    }

    /// <summary>
    /// Build the <see cref="RtcConnection"/> instance.
    /// </summary>
    /// <returns>A new <see cref="RtcConnection"/> instance.</returns>
    public RtcConnection Build()
    {
        var config = new RtcConnectionConfig
        {
            StunServers = _stunServers.Count > 0 ? _stunServers.ToArray() : new[] { "stun:stun.l.google.com:19302" },
            TurnServerUrl = _turnServerUrl,
            TurnUsername = _turnUsername,
            TurnPassword = _turnPassword,
            IceConnectionTimeoutMs = _iceConnectionTimeoutMs,
            DataChannelReliable = _dataChannelReliable
        };

        return new RtcConnection(config);
    }
}

/// <summary>
/// Configuration for <see cref="RtcConnection"/>.
/// </summary>
internal sealed class RtcConnectionConfig
{
    public string[] StunServers { get; init; } = Array.Empty<string>();
    public string? TurnServerUrl { get; init; }
    public string? TurnUsername { get; init; }
    public string? TurnPassword { get; init; }
    public int IceConnectionTimeoutMs { get; init; }
    public bool DataChannelReliable { get; init; }
}
