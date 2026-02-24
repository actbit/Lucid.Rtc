using System.Text.Json;
using Xunit;

namespace Lucid.Rtc.Tests;

public class RtcConfigTests
{
    [Fact]
    public void DefaultConfig_HasCorrectDefaultValues()
    {
        var config = new RtcConfig();

        Assert.NotNull(config.StunServers);
        Assert.Single(config.StunServers);
        Assert.Equal("stun:stun.l.google.com:19302", config.StunServers[0]);
        Assert.Null(config.TurnServerUrl);
        Assert.Null(config.TurnUsername);
        Assert.Null(config.TurnPassword);
        Assert.Equal(30000, config.IceConnectionTimeoutMs);
        Assert.True(config.DataChannelReliable);
    }

    [Fact]
    public void Config_CanBeCreatedWithCustomValues()
    {
        var config = new RtcConfig
        {
            StunServers = new[] { "stun:stun.example.com:3478" },
            TurnServerUrl = "turn:turn.example.com:3478",
            TurnUsername = "user",
            TurnPassword = "pass",
            IceConnectionTimeoutMs = 60000,
            DataChannelReliable = false
        };

        Assert.Single(config.StunServers);
        Assert.Equal("stun:stun.example.com:3478", config.StunServers[0]);
        Assert.Equal("turn:turn.example.com:3478", config.TurnServerUrl);
        Assert.Equal("user", config.TurnUsername);
        Assert.Equal("pass", config.TurnPassword);
        Assert.Equal(60000, config.IceConnectionTimeoutMs);
        Assert.False(config.DataChannelReliable);
    }

    [Fact]
    public void Config_CanBeSerializedToJson()
    {
        var config = new RtcConfig
        {
            StunServers = new[] { "stun:stun.example.com:3478" },
            TurnServerUrl = "turn:turn.example.com:3478",
            TurnUsername = "user",
            TurnPassword = "pass",
            IceConnectionTimeoutMs = 60000,
            DataChannelReliable = false
        };

        // Serialize in the format expected by the native library
        var json = JsonSerializer.Serialize(new
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
        });

        Assert.Contains("\"stun_servers\"", json);
        Assert.Contains("\"turn_server\"", json);
        Assert.Contains("\"ice_timeout_ms\"", json);
        Assert.Contains("\"data_channel_reliable\"", json);
    }

    [Fact]
    public void Config_WithMultipleStunServers_WorksCorrectly()
    {
        var config = new RtcConfig
        {
            StunServers = new[]
            {
                "stun:stun1.example.com:3478",
                "stun:stun2.example.com:3478",
                "stun:stun3.example.com:3478"
            }
        };

        Assert.Equal(3, config.StunServers.Length);
        Assert.Equal("stun:stun1.example.com:3478", config.StunServers[0]);
        Assert.Equal("stun:stun2.example.com:3478", config.StunServers[1]);
        Assert.Equal("stun:stun3.example.com:3478", config.StunServers[2]);
    }
}
