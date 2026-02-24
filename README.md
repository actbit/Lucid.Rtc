# Lucid.Rtc

**English** | [日本語](./README-ja.md)

**WebRTC for .NET** - Cross-platform, multi-backend WebRTC bindings with a modern fluent API.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![NuGet](https://img.shields.io/nuget/v/Lucid.Rtc.Core.svg)](https://www.nuget.org/packages/Lucid.Rtc.Core/)

## Features

- 🚀 **Modern Fluent API** - SignalR-inspired, method chaining support
- 🌍 **Cross-Platform** - Windows, Linux, macOS (x64, ARM64, ARM)
- 🔧 **Multiple Backends** - Rust (lightweight) and Pion/Go (media-ready)
- 📦 **Modular Packages** - Use only what you need
- 🔄 **MessagePack Support** - Optional serialization package
- 📡 **DataChannel** - Reliable P2P messaging
- 🎥 **Media Support** - Audio/Video (Pion backend only)

## Quick Start

### Installation

```xml
<!-- Core library + Rust backend (recommended for DataChannel) -->
<PackageReference Include="Lucid.Rtc" Version="0.1.0" />

<!-- Or with media support (Pion backend) -->
<PackageReference Include="Lucid.Rtc.Core" Version="0.1.0" />
<PackageReference Include="Lucid.Rtc.Pion.win-x64" Version="0.1.0" />

<!-- Optional: MessagePack serialization -->
<PackageReference Include="Lucid.Rtc.MessagePack" Version="0.1.0" />
```

### Basic Usage

```csharp
using Lucid.Rtc;

// Create connection
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .Build();

// Register event handlers (method chaining)
connection
    .On<PeerConnectedEvent>(e => Console.WriteLine($"Connected: {e.PeerId}"))
    .On<MessageReceivedEvent>(e => Console.WriteLine($"Message: {e.DataAsString}"))
    .On<IceCandidateEvent>(e => SendToSignaling(e.Candidate));

// Create peer with media support
var peer = await connection.CreatePeerAsync("remote-peer")
    .WithVideo("vp8")
    .WithAudio("opus");

// Negotiate
peer.SetRemoteOffer(offerSdp);

// Send data
peer.Send("Hello World!");
peer.SendVideo(rtpData);

// Cleanup
await peer.CloseAsync();
await connection.DisposeAsync();
```

---

## API Documentation

Detailed API documentation is available:

- [High-Level API](./docs/api/high-level.md) - RtcConnection, Peer, Events
- [Low-Level API](./docs/api/low-level.md) - RtcClient, MediaClient
- [Common Types](./docs/api/types.md) - IceCandidate, MediaCodec, etc.
- [MessagePack Extensions](./docs/api/messagepack.md) - Object serialization

### Samples

- [HighLevelSample](./dotnet/samples/HighLevelSample/) - Fluent API sample
- [LowLevelSample](./dotnet/samples/LowLevelSample/) - Low-level API sample

### RtcConnectionBuilder

Fluent builder for creating connections:

```csharp
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .WithStunServer("stun:stun1.l.google.com:19302")  // Multiple servers
    .WithTurnServer("turn:example.com:3478", "user", "pass")
    .WithIceConnectionTimeout(30000)
    .WithDataChannelReliable(true)
    .Build();
```

### RtcConnection

Main connection class with event handling:

```csharp
// Events (method chaining supported)
connection
    .On<PeerConnectedEvent>(e => { })
    .On<PeerDisconnectedEvent>(e => { })
    .On<MessageReceivedEvent>(e => { })
    .On<IceCandidateEvent>(e => { })
    .On<OfferReadyEvent>(e => { })
    .On<AnswerReadyEvent>(e => { })
    .On<DataChannelOpenEvent>(e => { })
    .On<DataChannelClosedEvent>(e => { })
    .On<VideoFrameEvent>(e => { })      // Pion only
    .On<AudioFrameEvent>(e => { })      // Pion only
    .On<ErrorEvent>(e => { });

// Peer management
var peer = await connection.CreatePeerAsync("peer-id");
var existingPeer = connection.GetPeer("peer-id");
var allPeers = connection.GetConnectedPeers();

// Broadcast
connection.Broadcast("Hello everyone!");
connection.Broadcast(binaryData);
```

### Peer

Represents a peer connection:

```csharp
// Properties
peer.Id              // "peer-id"
peer.IsConnected     // true/false
peer.State           // Connecting, Connected, Disconnected, Failed, Closed
peer.IsVideoEnabled  // true/false
peer.IsAudioEnabled  // true/false

// Configuration (method chaining supported)
peer.WithVideo("vp8")      // "vp8", "vp9", "h264", "av1"
peer.WithAudio("opus")     // "opus", "pcmu", "pcma"

// SDP negotiation (method chaining supported)
peer.SetRemoteOffer(sdp)
peer.SetRemoteAnswer(sdp)
peer.AddIceCandidate(candidate)

// Send data (no chaining - just fire)
peer.Send("text message");
peer.Send(binaryData);
peer.SendVideo(rtpPacket);   // Pion only
peer.SendAudio(rtpPacket);   // Pion only

// Close
await peer.CloseAsync();
```

### Events

| Event | Description | Properties |
|-------|-------------|------------|
| `PeerConnectedEvent` | Peer connected | `PeerId`, `Peer` |
| `PeerDisconnectedEvent` | Peer disconnected | `PeerId`, `Peer` |
| `MessageReceivedEvent` | Data received | `PeerId`, `Peer`, `Data`, `DataAsString` |
| `IceCandidateEvent` | ICE candidate ready | `PeerId`, `Peer`, `Candidate` |
| `OfferReadyEvent` | SDP offer ready | `PeerId`, `Peer`, `Sdp` |
| `AnswerReadyEvent` | SDP answer ready | `PeerId`, `Peer`, `Sdp` |
| `DataChannelOpenEvent` | DataChannel opened | `PeerId`, `Peer` |
| `DataChannelClosedEvent` | DataChannel closed | `PeerId`, `Peer` |
| `VideoFrameEvent` | Video frame received | `PeerId`, `Peer`, `Data` |
| `AudioFrameEvent` | Audio frame received | `PeerId`, `Peer`, `Data` |
| `ErrorEvent` | Error occurred | `Message` |

---

## MessagePack Serialization

Optional package for strongly-typed object serialization:

```xml
<PackageReference Include="Lucid.Rtc.MessagePack" Version="0.1.0" />
```

```csharp
using Lucid.Rtc;

// Define message types
[MessagePackObject]
public class ChatMessage
{
    [Key(0)] public string User { get; set; } = "";
    [Key(1)] public string Text { get; set; } = "";
}

// Send objects
peer.SendObject(new ChatMessage { User = "Alice", Text = "Hello!" });
connection.BroadcastObject(new ChatMessage { User = "System", Text = "Welcome!" });

// Receive objects
connection.OnObject<ChatMessage>(e =>
{
    Console.WriteLine($"{e.Value.User}: {e.Value.Text}");
});

// Optional: Configure compression
RtcMessagePackExtensions.Options = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.Lz4Block);
```

---

## Backend Comparison

| Feature | Rust (webrtc-rs) | Pion (Go) |
|---------|------------------|-----------|
| DataChannel | ✅ | ✅ |
| Audio Codecs | ❌ | Opus, G722, PCMU, PCMA |
| Video Codecs | ❌ | VP8, VP9, H264, AV1 |
| Simulcast | ❌ | ✅ |
| Binary Size | ~5MB | ~18MB |
| Maturity | Experimental | Production-ready |

**Recommendation**: Use **Rust** for DataChannel-only apps. Use **Pion** for audio/video.

---

## Package Structure

```
Lucid.Rtc                    # Metapackage (Core + Rust all platforms)
├── Lucid.Rtc.Core           # Core library (required)
├── Lucid.Rtc.MessagePack    # Optional: MessagePack support

Lucid.Rtc.Rust               # Rust backend packages
├── Lucid.Rtc.Rust.win-x64
├── Lucid.Rtc.Rust.win-x86
├── Lucid.Rtc.Rust.win-arm64
├── Lucid.Rtc.Rust.linux-x64
├── Lucid.Rtc.Rust.linux-arm64
├── Lucid.Rtc.Rust.linux-arm
├── Lucid.Rtc.Rust.osx-x64
├── Lucid.Rtc.Rust.osx-arm64
└── Lucid.Rtc.Rust.All       # All platforms

Lucid.Rtc.Pion               # Pion backend packages
├── Lucid.Rtc.Pion.win-x64
├── Lucid.Rtc.Pion.win-x86
├── Lucid.Rtc.Pion.win-arm64
├── Lucid.Rtc.Pion.linux-x64
├── Lucid.Rtc.Pion.linux-arm64
├── Lucid.Rtc.Pion.linux-arm
├── Lucid.Rtc.Pion.osx-x64
├── Lucid.Rtc.Pion.osx-arm64
└── Lucid.Rtc.Pion.All       # All platforms
```

---

## Complete Example: P2P Chat

```csharp
using Lucid.Rtc;

// Setup
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .Build();

connection
    .On<PeerConnectedEvent>(e => Console.WriteLine($"[{e.PeerId}] Connected"))
    .On<PeerDisconnectedEvent>(e => Console.WriteLine($"[{e.PeerId}] Disconnected"))
    .On<MessageReceivedEvent>(e => Console.WriteLine($"[{e.PeerId}] {e.DataAsString}"))
    .On<IceCandidateEvent>(e => signaling.SendCandidate(e.PeerId, e.Candidate));

// Offerer side
var peer = await connection.CreatePeerAsync("bob");
// peer.SetRemoteAnswer(answerFromSignaling);

// Answerer side
// var peer = connection.GetPeer("alice");
// peer.SetRemoteOffer(offerFromSignaling);

// Send messages
while (true)
{
    var input = Console.ReadLine();
    if (input == "quit") break;
    peer.Send(input);
}

// Cleanup
await peer.CloseAsync();
await connection.DisposeAsync();
```

---

## Building from Source

### Prerequisites

- **.NET 10.0 SDK**
- **Rust (stable)** - for Rust backend
- **Go 1.21+** + **GCC/MinGW** - for Pion backend

### Build Commands

```bash
# Build .NET solution
dotnet build

# Run tests
dotnet test

# Create NuGet packages
dotnet pack -c Release -o ./artifacts

# Build Rust backend
./build.ps1 -Target x86_64-pc-windows-msvc -Pack  # Windows
./build.sh -t x86_64-unknown-linux-gnu -p         # Linux/macOS

# Build Pion backend (requires Go + GCC)
cd pion && CGO_ENABLED=1 go build -buildmode=c-shared -o lucid_rtc.dll .
```

---

## Project Structure

```
Lucid.Rtc/
├── crates/
│   ├── lucid-rtc/              # Rust WebRTC implementation
│   └── lucid-rtc-sys/          # FFI bindings (C ABI)
├── pion/
│   ├── go.mod
│   ├── client.go               # Go WebRTC client
│   ├── exports.go              # C ABI exports
│   └── media.go                # Media track support
├── dotnet/
│   ├── Lucid.Rtc.Core/         # Core C# library
│   ├── Lucid.Rtc.MessagePack/  # MessagePack extensions
│   ├── Lucid.Rtc.Rust/         # Rust native packages
│   ├── Lucid.Rtc.Pion/         # Pion native packages
│   ├── Lucid.Rtc/              # Metapackage
│   ├── Lucid.Rtc.Tests/        # Unit tests
│   └── samples/                # Sample projects
│       ├── HighLevelSample/
│       └── LowLevelSample/
├── docs/api/                   # API documentation
│   ├── ja/                     # Japanese docs
│   ├── high-level.md
│   ├── low-level.md
│   ├── types.md
│   └── messagepack.md
├── build.ps1 / build.sh        # Build scripts
└── .github/workflows/          # CI/CD
```

---

## Low-Level API

For fine-grained control, use the low-level `RtcClient` API:

```csharp
// Low-level API (RtcClient)
var config = new RtcConfig
{
    StunServers = new[] { "stun:stun.l.google.com:19302" }
};

var client = new RtcClient(config);

// Event polling
client.EventReceived += (s, e) =>
{
    switch (e.Type)
    {
        case "message_received":
            Console.WriteLine($"Message: {Encoding.UTF8.GetString(e.Message!)}");
            break;
    }
};

// Manual polling (alternative)
while (client.TryGetEvent(out var evt))
{
    HandleEvent(evt);
}

// Synchronous operations
var offer = client.CreateOffer("peer1");
client.SetRemoteAnswer("peer1", answer);
client.SendMessage("peer1", data);
```

### API Comparison

| Feature | High-Level (RtcConnection) | Low-Level (RtcClient) |
|---------|---------------------------|----------------------|
| Style | Fluent, async | Classic, sync |
| Events | Typed (`On<T>`) | String-based (`evt.Type`) |
| Chaining | ✅ Supported | ❌ |
| Media | Integrated | Separate `MediaClient` |
| Control | Abstracted | Fine-grained |

---

## License

MIT License - see [LICENSE.txt](LICENSE.txt)
