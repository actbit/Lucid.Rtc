# Lucid.Rtc

WebRTC bindings for .NET with native P2P communication support.

## Features

- Cross-platform support (Windows, Linux, macOS)
- Native WebRTC implementation via [webrtc-rs](https://github.com/webrtc-rs/webrtc-rs)
- Simple C# API for peer-to-peer communication
- DataChannel support for reliable messaging
- STUN/TURN server configuration
- Event-driven architecture

## Project Structure

```
Lucid.Rtc/
├── Cargo.toml                    # Rust workspace configuration
├── crates/
│   ├── webrtc-sharp/             # Core Rust library
│   │   ├── Cargo.toml
│   │   └── src/
│   │       ├── lib.rs
│   │       ├── peer.rs
│   │       ├── data_channel.rs
│   │       ├── ice.rs
│   │       ├── signaling.rs
│   │       ├── error.rs
│   │       └── config.rs
│   └── webrtc-sharp-sys/         # FFI bindings (DLL/SO/DYLIB)
│       ├── Cargo.toml
│       └── src/
│           ├── lib.rs
│           └── helpers.rs
├── dotnet/
│   ├── Lucid.Rtc/                # C# wrapper (NuGet package)
│   │   ├── Lucid.Rtc.csproj
│   │   ├── Native/
│   │   │   └── NativeMethods.cs
│   │   ├── RtcClient.cs
│   │   ├── RtcConfig.cs
│   │   └── RtcEvent.cs
│   └── Lucid.Rtc.Tests/          # Unit tests
│       ├── Lucid.Rtc.Tests.csproj
│       ├── RtcConfigTests.cs
│       ├── RtcEventTests.cs
│       └── IceCandidateTests.cs
├── examples/
├── Lucid.Rtc.slnx                # Visual Studio solution
└── README.md
```

## Building

### Prerequisites

- Rust (stable)
- .NET 10.0 SDK

### Build Native Library

```bash
# Build for current platform
cargo build --release -p webrtc-sharp-sys

# Build for specific target
cargo build --release -p webrtc-sharp-sys --target x86_64-pc-windows-msvc
cargo build --release -p webrtc-sharp-sys --target aarch64-pc-windows-msvc
cargo build --release -p webrtc-sharp-sys --target x86_64-unknown-linux-gnu
cargo build --release -p webrtc-sharp-sys --target aarch64-unknown-linux-gnu
cargo build --release -p webrtc-sharp-sys --target x86_64-apple-darwin
cargo build --release -p webrtc-sharp-sys --target aarch64-apple-darwin
```

### Copy Native Libraries

```bash
# Windows x64
cp target/x86_64-pc-windows-msvc/release/webrtc_sharp.dll dotnet/Lucid.Rtc/runtimes/win-x64/native/

# Linux x64
cp target/x86_64-unknown-linux-gnu/release/libwebrtc_sharp.so dotnet/Lucid.Rtc/runtimes/linux-x64/native/

# macOS ARM64
cp target/aarch64-apple-darwin/release/libwebrtc_sharp.dylib dotnet/Lucid.Rtc/runtimes/osx-arm64/native/
```

### Build Solution

```bash
dotnet build Lucid.Rtc.slnx
```

### Run Tests

```bash
dotnet test
```

### Build NuGet Package

```bash
cd dotnet/Lucid.Rtc
dotnet pack -c Release
```

## Installation

### Via NuGet

```xml
<PackageReference Include="Lucid.Rtc" Version="0.1.0" />
```

## Usage

### Create Client

```csharp
using Lucid.Rtc;

var config = new RtcConfig
{
    StunServers = new[] { "stun:stun.l.google.com:19302" }
};

using var client = new RtcClient(config);

client.EventReceived += (sender, evt) =>
{
    Console.WriteLine($"Event: {evt.Type}");
};

// Create offer
var offer = client.CreateOffer("peer1");
Console.WriteLine($"Offer: {offer}");
```

### Handle Events

```csharp
while (client.TryGetEvent(out var evt))
{
    switch (evt?.Type)
    {
        case "message_received":
            var data = Convert.FromBase64String(evt.Data!);
            Console.WriteLine($"Message from {evt.PeerId}: {Encoding.UTF8.GetString(data)}");
            break;

        case "ice_candidate":
            // Send ICE candidate to signaling server
            signalingServer.SendIceCandidate(evt.PeerId, evt.Candidate);
            break;

        case "peer_connected":
            Console.WriteLine($"Peer connected: {evt.PeerId}");
            break;

        case "data_channel_open":
            // Ready to send messages
            client.SendMessage(evt.PeerId!, "Hello!");
            break;
    }
}
```

### Complete P2P Flow

#### Offerer Side

```csharp
// 1. Create offer
var offer = client.CreateOffer("peer1");

// 2. Send offer to signaling server
await signalingServer.SendOfferAsync("peer1", offer);

// 3. Wait for answer (via signaling)
// signalingServer.OnAnswer += (peerId, answer) => client.SetRemoteAnswer(peerId, answer);

// 4. Exchange ICE candidates
// signalingServer.OnIceCandidate += (peerId, candidate) =>
//     client.AddIceCandidate(peerId, candidate);
```

#### Answerer Side

```csharp
// 1. Receive offer from signaling
// signalingServer.OnOffer += async (peerId, offer) =>
// {
//     // 2. Create answer
//     var answer = client.SetRemoteOffer(peerId, offer);
//
//     // 3. Send answer back
//     await signalingServer.SendAnswerAsync(peerId, answer);
// };

// 4. Exchange ICE candidates
// signalingServer.OnIceCandidate += (peerId, candidate) =>
//     client.AddIceCandidate(peerId, candidate);
```

## API Reference

### RtcConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| StunServers | string[] | Google STUN | STUN server URLs |
| TurnServerUrl | string? | null | TURN server URL |
| TurnUsername | string? | null | TURN username |
| TurnPassword | string? | null | TURN password |
| IceConnectionTimeoutMs | int | 30000 | ICE timeout (ms) |
| DataChannelReliable | bool | true | DataChannel reliability |

### RtcClient

| Method | Description |
|--------|-------------|
| RtcClient(config) | Create a new client with optional configuration |
| CreateOffer(peerId) | Create SDP offer |
| SetRemoteOffer(peerId, sdp) | Set remote offer and get answer |
| SetRemoteAnswer(peerId, sdp) | Set remote answer |
| AddIceCandidate(...) | Add ICE candidate |
| SendMessage(peerId, data) | Send binary data |
| SendMessage(peerId, message) | Send string message |
| IsConnected(peerId) | Check connection state |
| WaitForIceConnected(peerId) | Wait for ICE connection |
| ClosePeer(peerId) | Close peer connection |
| TryGetEvent(out evt) | Get next event |
| IsNativeAvailable | Check if native library is loaded |

### RtcEvent Types

| Type | Description |
|------|-------------|
| peer_connected | Peer connected |
| peer_disconnected | Peer disconnected |
| message_received | Message received (base64 data) |
| ice_candidate | ICE candidate generated |
| offer_ready | SDP offer ready |
| answer_ready | SDP answer ready |
| ice_connection_state_change | ICE state changed |
| data_channel_open | DataChannel opened |
| data_channel_closed | DataChannel closed |
| error | Error occurred |

## License

MIT
