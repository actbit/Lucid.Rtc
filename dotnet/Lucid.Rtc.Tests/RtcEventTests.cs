using Xunit;

namespace Lucid.Rtc.Tests;

public class RtcEventTests
{
    [Fact]
    public void ParseEvents_EmptyArray_ReturnsEmptyArray()
    {
        var json = "[]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Empty(events);
    }

    [Fact]
    public void ParseEvents_PeerConnectedEvent_ParsesCorrectly()
    {
        var json = @"[{""type"":""peer_connected"",""peer_id"":""peer1""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("peer_connected", evt.Type);
        Assert.Equal("peer1", evt.PeerId);
    }

    [Fact]
    public void ParseEvents_PeerDisconnectedEvent_ParsesCorrectly()
    {
        var json = @"[{""type"":""peer_disconnected"",""peer_id"":""peer2""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("peer_disconnected", evt.Type);
        Assert.Equal("peer2", evt.PeerId);
    }

    [Fact]
    public void ParseEvents_MessageReceivedEvent_ParsesCorrectly()
    {
        var json = @"[{""type"":""message_received"",""peer_id"":""peer1"",""data"":""SGVsbG8gV29ybGQ=""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("message_received", evt.Type);
        Assert.Equal("peer1", evt.PeerId);
        Assert.Equal("SGVsbG8gV29ybGQ=", evt.Data);
    }

    [Fact]
    public void ParseEvents_IceCandidateEvent_ParsesCorrectly()
    {
        var json = @"[{""type"":""ice_candidate"",""peer_id"":""peer1"",""candidate"":{""candidate"":""candidate:1 1 UDP 2122260223 192.168.1.1 54321 typ host"",""sdp_mid"":""0"",""sdp_mline_index"":0}}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("ice_candidate", evt.Type);
        Assert.Equal("peer1", evt.PeerId);
        Assert.NotNull(evt.Candidate);
        Assert.Equal("candidate:1 1 UDP 2122260223 192.168.1.1 54321 typ host", evt.Candidate.Candidate);
        Assert.Equal("0", evt.Candidate.SdpMid);
        Assert.Equal(0, evt.Candidate.SdpMlineIndex);
    }

    [Fact]
    public void ParseEvents_OfferReadyEvent_ParsesCorrectly()
    {
        var json = @"[{""type"":""offer_ready"",""peer_id"":""peer1"",""sdp"":""v=0\r\no=- 123456 2 IN IP4 127.0.0.1\r\n""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("offer_ready", evt.Type);
        Assert.Equal("peer1", evt.PeerId);
        Assert.Equal("v=0\r\no=- 123456 2 IN IP4 127.0.0.1\r\n", evt.Sdp);
    }

    [Fact]
    public void ParseEvents_AnswerReadyEvent_ParsesCorrectly()
    {
        var json = @"[{""type"":""answer_ready"",""peer_id"":""peer2"",""sdp"":""v=0\r\no=- 789012 2 IN IP4 127.0.0.1\r\n""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("answer_ready", evt.Type);
        Assert.Equal("peer2", evt.PeerId);
        Assert.Equal("v=0\r\no=- 789012 2 IN IP4 127.0.0.1\r\n", evt.Sdp);
    }

    [Fact]
    public void ParseEvents_IceConnectionStateChangeEvent_ParsesCorrectly()
    {
        var json = @"[{""type"":""ice_connection_state_change"",""peer_id"":""peer1"",""state"":""connected""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("ice_connection_state_change", evt.Type);
        Assert.Equal("peer1", evt.PeerId);
        Assert.Equal("connected", evt.State);
    }

    [Fact]
    public void ParseEvents_DataChannelOpenEvent_ParsesCorrectly()
    {
        var json = @"[{""type"":""data_channel_open"",""peer_id"":""peer1""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("data_channel_open", evt.Type);
        Assert.Equal("peer1", evt.PeerId);
    }

    [Fact]
    public void ParseEvents_DataChannelClosedEvent_ParsesCorrectly()
    {
        var json = @"[{""type"":""data_channel_closed"",""peer_id"":""peer1""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("data_channel_closed", evt.Type);
        Assert.Equal("peer1", evt.PeerId);
    }

    [Fact]
    public void ParseEvents_ErrorEvent_ParsesCorrectly()
    {
        var json = @"[{""type"":""error"",""message"":""Connection failed""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("error", evt.Type);
        Assert.Equal("Connection failed", evt.ErrorMessage);
    }

    [Fact]
    public void ParseEvents_UnknownEvent_ParsesWithDefaultType()
    {
        var json = @"[{""type"":""unknown_event"",""peer_id"":""peer1""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("unknown_event", evt.Type);
    }

    [Fact]
    public void ParseEvents_EventWithoutType_ReturnsNull()
    {
        var json = @"[{""peer_id"":""peer1""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Empty(events);
    }

    [Fact]
    public void ParseEvents_MultipleEvents_ParsesAllCorrectly()
    {
        var json = @"[{""type"":""peer_connected"",""peer_id"":""peer1""},{""type"":""data_channel_open"",""peer_id"":""peer1""},{""type"":""message_received"",""peer_id"":""peer1"",""data"":""SGVsbG8=""},{""type"":""peer_disconnected"",""peer_id"":""peer1""}]";
        var events = RtcEventConverter.ParseEvents(json);

        Assert.Equal(4, events.Length);
        Assert.Equal("peer_connected", events[0].Type);
        Assert.Equal("data_channel_open", events[1].Type);
        Assert.Equal("message_received", events[2].Type);
        Assert.Equal("peer_disconnected", events[3].Type);
    }
}
