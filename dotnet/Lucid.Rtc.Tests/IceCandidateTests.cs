using Xunit;

namespace Lucid.Rtc.Tests;

public class IceCandidateTests
{
    [Fact]
    public void IceCandidate_CanBeCreatedWithRequiredProperties()
    {
        var candidate = new IceCandidate
        {
            Candidate = "candidate:1 1 UDP 2122260223 192.168.1.1 54321 typ host",
            SdpMid = "0",
            SdpMlineIndex = 0
        };

        Assert.Equal("candidate:1 1 UDP 2122260223 192.168.1.1 54321 typ host", candidate.Candidate);
        Assert.Equal("0", candidate.SdpMid);
        Assert.Equal(0, candidate.SdpMlineIndex);
    }

    [Fact]
    public void IceCandidate_WithDifferentSdpMlineIndex_WorksCorrectly()
    {
        var candidate = new IceCandidate
        {
            Candidate = "candidate:2 1 UDP 2122260223 192.168.1.2 54322 typ host",
            SdpMid = "1",
            SdpMlineIndex = 1
        };

        Assert.Equal(1, candidate.SdpMlineIndex);
        Assert.Equal("1", candidate.SdpMid);
    }

    [Fact]
    public void IceCandidate_WithSrflxType_ParsesCorrectly()
    {
        var candidate = new IceCandidate
        {
            Candidate = "candidate:3 1 UDP 1686052607 203.0.113.1 54323 typ srflx raddr 192.168.1.1 rport 54321",
            SdpMid = "0",
            SdpMlineIndex = 0
        };

        Assert.Contains("typ srflx", candidate.Candidate);
    }

    [Fact]
    public void IceCandidate_WithRelayType_ParsesCorrectly()
    {
        var candidate = new IceCandidate
        {
            Candidate = "candidate:4 1 UDP 1677721855 203.0.113.2 54324 typ relay raddr 203.0.113.1 rport 3478",
            SdpMid = "0",
            SdpMlineIndex = 0
        };

        Assert.Contains("typ relay", candidate.Candidate);
    }

    [Fact]
    public void IceCandidate_WithIpv6Address_WorksCorrectly()
    {
        var candidate = new IceCandidate
        {
            Candidate = "candidate:5 1 UDP 2122260223 2001:db8::1 54325 typ host",
            SdpMid = "0",
            SdpMlineIndex = 0
        };

        Assert.Contains("2001:db8::1", candidate.Candidate);
    }
}
