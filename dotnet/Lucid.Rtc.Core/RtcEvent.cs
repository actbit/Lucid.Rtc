using System.Text.Json;

namespace Lucid.Rtc;

/// <summary>
/// ICE candidate information.
/// </summary>
public sealed class IceCandidate
{
    /// <summary>
    /// The candidate string.
    /// </summary>
    public required string Candidate { get; init; }

    /// <summary>
    /// SDP media identifier.
    /// </summary>
    public required string SdpMid { get; init; }

    /// <summary>
    /// SDP media line index.
    /// </summary>
    public required int SdpMlineIndex { get; init; }
}

/// <summary>
/// RTC event types.
/// </summary>
public sealed class RtcEvent
{
    /// <summary>
    /// Event type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Peer ID (for peer events).
    /// </summary>
    public string? PeerId { get; init; }

    /// <summary>
    /// SDP (for offer/answer events).
    /// </summary>
    public string? Sdp { get; init; }

    /// <summary>
    /// Data (for message events, base64 encoded).
    /// </summary>
    public string? Data { get; init; }

    /// <summary>
    /// ICE candidate (for ICE events).
    /// </summary>
    public IceCandidate? Candidate { get; init; }

    /// <summary>
    /// State (for ICE connection state change events).
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Error message (for error events).
    /// </summary>
    public string? Message { get; init; }
}

internal sealed class RtcEventConverter
{
    public static RtcEvent[] ParseEvents(string json)
    {
        var events = new List<RtcEvent>();
        using var doc = JsonDocument.Parse(json);

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var evt = ParseEvent(element);
            if (evt != null)
                events.Add(evt);
        }

        return events.ToArray();
    }

    private static RtcEvent? ParseEvent(JsonElement element)
    {
        if (!element.TryGetProperty("type", out var typeProp))
            return null;

        var eventType = typeProp.GetString() ?? "";

        return eventType switch
        {
            "peer_connected" => ParsePeerEvent(element, eventType),
            "peer_disconnected" => ParsePeerEvent(element, eventType),
            "message_received" => ParseMessageEvent(element, eventType),
            "ice_candidate" => ParseIceCandidateEvent(element, eventType),
            "offer_ready" => ParseSdpEvent(element, eventType),
            "answer_ready" => ParseSdpEvent(element, eventType),
            "ice_connection_state_change" => ParseStateEvent(element, eventType),
            "data_channel_open" => ParsePeerEvent(element, eventType),
            "data_channel_closed" => ParsePeerEvent(element, eventType),
            "error" => ParseErrorEvent(element, eventType),
            _ => new RtcEvent { Type = eventType }
        };
    }

    private static RtcEvent ParsePeerEvent(JsonElement element, string type)
    {
        return new RtcEvent
        {
            Type = type,
            PeerId = element.GetProperty("peer_id").GetString()
        };
    }

    private static RtcEvent ParseMessageEvent(JsonElement element, string type)
    {
        return new RtcEvent
        {
            Type = type,
            PeerId = element.GetProperty("peer_id").GetString(),
            Data = element.GetProperty("data").GetString()
        };
    }

    private static RtcEvent ParseIceCandidateEvent(JsonElement element, string type)
    {
        var candidateElement = element.GetProperty("candidate");
        return new RtcEvent
        {
            Type = type,
            PeerId = element.GetProperty("peer_id").GetString(),
            Candidate = new IceCandidate
            {
                Candidate = candidateElement.GetProperty("candidate").GetString()!,
                SdpMid = candidateElement.GetProperty("sdp_mid").GetString()!,
                SdpMlineIndex = candidateElement.GetProperty("sdp_mline_index").GetInt32()!
            }
        };
    }

    private static RtcEvent ParseSdpEvent(JsonElement element, string type)
    {
        return new RtcEvent
        {
            Type = type,
            PeerId = element.GetProperty("peer_id").GetString(),
            Sdp = element.GetProperty("sdp").GetString()
        };
    }

    private static RtcEvent ParseStateEvent(JsonElement element, string type)
    {
        return new RtcEvent
        {
            Type = type,
            PeerId = element.GetProperty("peer_id").GetString(),
            State = element.GetProperty("state").GetString()
        };
    }

    private static RtcEvent ParseErrorEvent(JsonElement element, string type)
    {
        return new RtcEvent
        {
            Type = type,
            Message = element.GetProperty("message").GetString()
        };
    }
}
