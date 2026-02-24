# High-Level API

**English** | [日本語](./ja/high-level.md)

[← README](../../README.md) | [High-Level](high-level.md) | [Low-Level](low-level.md) | [Types](types.md) | [MessagePack](messagepack.md)

---

Modern fluent API for most use cases.

## RtcConnectionBuilder

Builder for creating `RtcConnection` instances.

### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `WithStunServer(string)` | `stunServer` - STUN server URL | `this` | Add a STUN server |
| `WithStunServers(params string[])` | `stunServers` - Array of URLs | `this` | Add multiple STUN servers |
| `WithTurnServer(string, string, string)` | `url`, `username`, `password` | `this` | Configure TURN server |
| `WithIceConnectionTimeout(int)` | `timeoutMs` (default: 30000) | `this` | Set ICE timeout |
| `WithDataChannelReliable(bool)` | `reliable` (default: true) | `this` | Set DataChannel reliability |
| `Build()` | - | `RtcConnection` | Create the connection |

### Example

```csharp
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .WithTurnServer("turn:example.com:3478", "user", "pass")
    .Build();
```

---

## RtcConnection

Main connection class for WebRTC communication.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsConnected` | `bool` | Whether native connection is available |
| `Version` | `string` (static) | Library version |

### Methods

#### Event Registration

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `On<T>(Action<T>)` | `handler` | `this` | Register typed event handler |
| `Off<T>()` | - | `this` | Remove all handlers for type T |

#### Peer Management

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `CreatePeerAsync(string)` | `peerId` | `Task<Peer>` | Create a new peer |
| `GetPeer(string)` | `peerId` | `Peer?` | Get existing peer |
| `GetConnectedPeers()` | - | `IEnumerable<Peer>` | Get all connected peers |

#### Messaging

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `Broadcast(string)` | `message` | `void` | Broadcast string to all peers |
| `Broadcast(byte[])` | `data` | `void` | Broadcast binary to all peers |

#### Lifecycle

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `DisposeAsync()` | - | `ValueTask` | Dispose connection |

### Example

```csharp
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .Build();

connection
    .On<PeerConnectedEvent>(e => Console.WriteLine($"Connected: {e.PeerId}"))
    .On<MessageReceivedEvent>(e => Console.WriteLine($"Message: {e.DataAsString}"))
    .On<IceCandidateEvent>(e => SendToSignaling(e.Candidate));

var peer = await connection.CreatePeerAsync("remote-peer");
connection.Broadcast("Hello!");

await connection.DisposeAsync();
```

---

## Peer

Represents a WebRTC peer connection.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Peer identifier |
| `IsConnected` | `bool` | Whether connected |
| `State` | `PeerState` | Connection state |
| `IsVideoEnabled` | `bool` | Video enabled |
| `IsAudioEnabled` | `bool` | Audio enabled |

### Methods

#### Configuration (Chainable)

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `WithVideo(VideoCodec)` | `codec` - `VideoCodec.Vp8`, `Vp9`, `H264`, `Av1` | `this` | Enable video (enum) |
| `WithVideo(string)` | `codec` - "vp8", "vp9", "h264", "av1" | `this` | Enable video (string) |
| `WithAudio(AudioCodec)` | `codec` - `AudioCodec.Opus`, `Pcmu`, `Pcma` | `this` | Enable audio (enum) |
| `WithAudio(string)` | `codec` - "opus", "pcmu", "pcma" | `this` | Enable audio (string) |

#### SDP Negotiation (Chainable)

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `SetRemoteOffer(string)` | `sdp` | `this` | Set remote offer |
| `SetRemoteAnswer(string)` | `sdp` | `this` | Set remote answer |
| `AddIceCandidate(IceCandidate)` | `candidate` | `this` | Add ICE candidate |

#### Messaging

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `Send(string)` | `message` | `void` | Send string |
| `Send(byte[])` | `data` | `void` | Send binary |
| `SendVideo(byte[])` | `rtpData` | `void` | Send video RTP (Pion) |
| `SendAudio(byte[])` | `rtpData` | `void` | Send audio RTP (Pion) |

#### Lifecycle

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `CloseAsync()` | - | `Task` | Close peer |

### Example

```csharp
// Using enums (recommended)
var peer = await connection.CreatePeerAsync("remote-peer")
    .WithVideo(VideoCodec.Vp8)
    .WithAudio(AudioCodec.Opus);

// Or using strings:
// .WithVideo("vp8").WithAudio("opus")

peer.SetRemoteOffer(remoteOfferSdp);
peer.AddIceCandidate(candidate);

peer.Send("Hello!");
peer.SendVideo(rtpPacket);

await peer.CloseAsync();
```

---

## VideoCodec

| Value | Description |
|-------|-------------|
| `Vp8` | VP8 codec (recommended) |
| `Vp9` | VP9 codec |
| `H264` | H.264 codec |
| `Av1` | AV1 codec |

---

## AudioCodec

| Value | Description |
|-------|-------------|
| `Opus` | Opus codec (recommended) |
| `Pcmu` | G.711 μ-law |
| `Pcma` | G.711 A-law |

---

## PeerState

| Value | Description |
|-------|-------------|
| `Connecting` | Being established |
| `Connected` | Ready for data |
| `Disconnected` | Disconnected |
| `Failed` | Connection failed |
| `Closed` | Closed |

---

## Events

All events inherit from `RtcEventBase`.

### RtcEventBase

| Property | Type | Description |
|----------|------|-------------|
| `PeerId` | `string?` | Peer ID |
| `Peer` | `Peer?` | Peer object |

### Event Types

| Event | Properties | Description |
|-------|------------|-------------|
| `PeerConnectedEvent` | - | Peer connected |
| `PeerDisconnectedEvent` | - | Peer disconnected |
| `MessageReceivedEvent` | `Data`, `DataAsString` | Data received |
| `IceCandidateEvent` | `Candidate` | ICE candidate ready |
| `OfferReadyEvent` | `Sdp` | SDP offer ready |
| `AnswerReadyEvent` | `Sdp` | SDP answer ready |
| `IceConnectionStateChangeEvent` | `State` | ICE state changed |
| `DataChannelOpenEvent` | - | DataChannel opened |
| `DataChannelClosedEvent` | - | DataChannel closed |
| `VideoFrameEvent` | `Data` | Video frame (Pion) |
| `AudioFrameEvent` | `Data` | Audio frame (Pion) |
| `ErrorEvent` | `Message` | Error occurred |

### Example

```csharp
connection
    .On<PeerConnectedEvent>(e => Console.WriteLine($"Connected: {e.PeerId}"))
    .On<MessageReceivedEvent>(e => Console.WriteLine($"{e.PeerId}: {e.DataAsString}"))
    .On<IceCandidateEvent>(e => signaling.Send(e.Candidate))
    .On<VideoFrameEvent>(e => DisplayFrame(e.Data))
    .On<ErrorEvent>(e => Console.WriteLine($"Error: {e.Message}"));
```
