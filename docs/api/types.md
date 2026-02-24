# Common Types

## IceCandidate

ICE candidate information.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Candidate` | `string` | Candidate string |
| `SdpMid` | `string` | SDP media ID |
| `SdpMlineIndex` | `int` | SDP media line index |

### Example

```csharp
var candidate = new IceCandidate
{
    Candidate = "candidate:1 1 UDP 2122260223 192.168.1.1 54321 typ host",
    SdpMid = "0",
    SdpMlineIndex = 0
};

// High-level API
peer.AddIceCandidate(candidate);

// Low-level API
client.AddIceCandidate("peer1", candidate);
```

---

## MediaCodec

Media codec information (Pion backend).

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `MimeType` | `string` | MIME type (e.g., "audio/opus", "video/VP8") |
| `ClockRate` | `uint` | Clock rate in Hz |
| `Channels` | `ushort` | Number of audio channels |
| `SdpFmtpLine` | `string?` | SDP format parameters |

### Example

```csharp
var codecs = MediaClient.GetSupportedCodecs();
foreach (var codec in codecs)
{
    Console.WriteLine($"{codec.MimeType} @ {codec.ClockRate}Hz");
}
// Output:
// audio/opus @ 48000Hz
// video/VP8 @ 90000Hz
// video/VP9 @ 90000Hz
// video/H264 @ 90000Hz
```

---

## MediaTrackConfig

Configuration for creating media tracks.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Kind` | `string` | "audio" or "video" |
| `Codec` | `string` | Codec name |
| `TrackId` | `string?` | Custom track ID (optional) |
| `StreamId` | `string?` | Custom stream ID (optional) |

### Supported Codecs

| Kind | Codec | Description |
|------|-------|-------------|
| audio | `opus` | Opus codec (recommended) |
| audio | `pcmu` | G.711 μ-law |
| audio | `pcma` | G.711 A-law |
| video | `vp8` | VP8 (recommended) |
| video | `vp9` | VP9 |
| video | `h264` | H.264 |
| video | `av1` | AV1 |

### Example

```csharp
// Video track
var videoConfig = new MediaTrackConfig
{
    Kind = "video",
    Codec = "vp8",
    TrackId = "video-0",
    StreamId = "stream-0"
};

// Audio track
var audioConfig = new MediaTrackConfig
{
    Kind = "audio",
    Codec = "opus"
};

var videoTrackId = client.CreateMediaTrack(videoConfig);
var audioTrackId = client.CreateMediaTrack(audioConfig);
```

---

## RtcConfig

Configuration for low-level `RtcClient`.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `StunServers` | `string[]?` | `["stun:stun.l.google.com:19302"]` | STUN server URLs |
| `TurnServerUrl` | `string?` | `null` | TURN server URL |
| `TurnUsername` | `string?` | `null` | TURN username |
| `TurnPassword` | `string?` | `null` | TURN password |
| `IceConnectionTimeoutMs` | `int` | `30000` | ICE timeout (ms) |
| `DataChannelReliable` | `bool` | `true` | DataChannel reliability |

### Example

```csharp
// Basic configuration
var config = new RtcConfig
{
    StunServers = new[] { "stun:stun.l.google.com:19302" }
};

// With TURN server
var config = new RtcConfig
{
    StunServers = new[]
    {
        "stun:stun.l.google.com:19302",
        "stun:stun1.l.google.com:19302"
    },
    TurnServerUrl = "turn:turn.example.com:3478",
    TurnUsername = "myuser",
    TurnPassword = "mypass",
    IceConnectionTimeoutMs = 60000,
    DataChannelReliable = true
};
```
