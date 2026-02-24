# Low-Level API

**English** | [日本語](./ja/low-level.md)

[← README](../../README.md) | [High-Level](high-level.md) | [Low-Level](low-level.md) | [Types](types.md) | [MessagePack](messagepack.md)

---

Direct FFI wrapper for fine-grained control.

## RtcConfig

Configuration for `RtcClient`.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `StunServers` | `string[]?` | Google STUN | STUN server URLs |
| `TurnServerUrl` | `string?` | `null` | TURN server URL |
| `TurnUsername` | `string?` | `null` | TURN username |
| `TurnPassword` | `string?` | `null` | TURN password |
| `IceConnectionTimeoutMs` | `int` | 30000 | ICE timeout (ms) |
| `DataChannelReliable` | `bool` | `true` | DataChannel reliability |

### Example

```csharp
var config = new RtcConfig
{
    StunServers = new[] { "stun:stun.l.google.com:19302" },
    TurnServerUrl = "turn:example.com:3478",
    TurnUsername = "user",
    TurnPassword = "pass"
};
```

---

## RtcClient

Low-level WebRTC client.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsNativeAvailable` | `bool` | Native library loaded |
| `EventReceived` | `event` | RTC event handler |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetVersion()` | `string` | Library version |

### Instance Methods

#### Lifecycle

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `RtcClient(RtcConfig?)` | `config` | - | Create client |
| `Dispose()` | - | `void` | Dispose client |

#### SDP Negotiation

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `CreateOffer(string)` | `peerId` | `string?` | Create SDP offer |
| `SetRemoteOffer(string, string)` | `peerId`, `sdp` | `string?` | Set offer, get answer |
| `SetRemoteAnswer(string, string)` | `peerId`, `sdp` | `bool` | Set remote answer |

#### ICE

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `AddIceCandidate(string, string, string, int)` | `peerId`, `candidate`, `sdpMid`, `sdpMlineIndex` | `bool` | Add ICE candidate |
| `AddIceCandidate(string, IceCandidate)` | `peerId`, `candidate` | `bool` | Add ICE (object) |

#### Messaging

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `SendMessage(string, byte[])` | `peerId`, `data` | `bool` | Send binary |
| `SendMessage(string, string)` | `peerId`, `message` | `bool` | Send string |
| `Broadcast(byte[])` | `data` | `bool` | Broadcast binary |
| `Broadcast(string)` | `message` | `bool` | Broadcast string |

#### State

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `IsConnected(string)` | `peerId` | `bool` | Check connection |
| `WaitForIceConnected(string)` | `peerId` | `bool` | Wait for ICE |

#### Peer Management

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `ClosePeer(string)` | `peerId` | `bool` | Close peer |
| `CloseAllPeers()` | - | `bool` | Close all |

#### Events

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `TryGetEvent(out RtcEvent?)` | `evt` | `bool` | Poll event queue |

### Example

```csharp
using var client = new RtcClient(config);

// Event-based
client.EventReceived += (sender, evt) =>
{
    switch (evt.Type)
    {
        case "message_received":
            Console.WriteLine($"Message: {Encoding.UTF8.GetString(evt.Message!)}");
            break;
        case "ice_candidate":
            SendToSignaling(evt.PeerId!, evt.Candidate!);
            break;
    }
};

// Polling-based
while (running)
{
    if (client.TryGetEvent(out var evt))
        HandleEvent(evt);
    Thread.Sleep(10);
}

// Operations
var offer = client.CreateOffer("peer1");
client.SetRemoteAnswer("peer1", answerSdp);
client.SendMessage("peer1", "Hello!");
```

---

## MediaClient

Extended client with media support (Pion backend only).

### Static Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsPionBackend` | `bool` | Current backend is Pion |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetSupportedCodecs()` | `MediaCodec[]` | Supported codecs |

### Instance Methods

Inherits all from `RtcClient`, plus:

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `CreateMediaTrack(MediaTrackConfig)` | `config` | `string?` | Create track, return ID |
| `AddTrackToPeer(string, string)` | `peerId`, `trackId` | `bool` | Add track to peer |
| `SendMediaData(string, byte[])` | `trackId`, `rtpData` | `bool` | Send RTP data |
| `RemoveMediaTrack(string)` | `trackId` | `bool` | Remove track |

### Example

```csharp
if (!MediaClient.IsPionBackend)
    return;

using var client = new MediaClient(config);

// Get codecs
var codecs = MediaClient.GetSupportedCodecs();

// Create tracks
var videoTrack = client.CreateMediaTrack(new MediaTrackConfig
{
    Kind = "video",
    Codec = "vp8"
});

var audioTrack = client.CreateMediaTrack(new MediaTrackConfig
{
    Kind = "audio",
    Codec = "opus"
});

// Add to peer
client.AddTrackToPeer("peer1", videoTrack!);
client.AddTrackToPeer("peer1", audioTrack!);

// Send RTP
client.SendMediaData(videoTrack!, rtpPacket);

// Cleanup
client.RemoveMediaTrack(videoTrack!);
```

---

## RtcEvent

Event class for low-level API.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Type` | `string` | Event type string |
| `PeerId` | `string?` | Peer ID |
| `Sdp` | `string?` | SDP (offer/answer) |
| `Data` | `string?` | Base64 data |
| `Message` | `byte[]?` | Decoded bytes |
| `Candidate` | `IceCandidate?` | ICE candidate |
| `State` | `string?` | State string |
| `ErrorMessage` | `string?` | Error message |

### Event Types

| Type | Description |
|------|-------------|
| `"peer_connected"` | Peer connected |
| `"peer_disconnected"` | Peer disconnected |
| `"message_received"` | Data received |
| `"ice_candidate"` | ICE candidate ready |
| `"offer_ready"` | SDP offer ready |
| `"answer_ready"` | SDP answer ready |
| `"ice_connection_state_change"` | ICE state changed |
| `"data_channel_open"` | DataChannel opened |
| `"data_channel_closed"` | DataChannel closed |
| `"error"` | Error occurred |
