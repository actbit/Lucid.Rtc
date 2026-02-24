# Lucid.Rtc

WebRTC bindings for .NET with native P2P communication support.

## Features

- Cross-platform support (Windows, Linux, macOS)
- Multiple backends: **Rust (webrtc-rs)** and **Pion (Go)**
- Simple C# API for peer-to-peer communication
- DataChannel support for reliable messaging
- STUN/TURN server configuration
- Event-driven architecture
- **Pion-only: Media Track support (Audio/Video)**

## Backend Comparison

| Feature | Rust (webrtc-rs) | Pion (Go) |
|---------|------------------|-----------|
| DataChannel | ✅ | ✅ |
| Audio Codecs | ❌ | Opus, G722, PCMU, PCMA |
| Video Codecs | ❌ | VP8, VP9, H264, AV1 |
| Simulcast | ❌ | ✅ |
| Binary Size | Smaller | Larger |
| Maturity | Experimental | Production-ready |

## Package Structure

```
Lucid.Rtc.Core              # Core managed library (required)

Lucid.Rtc.Rust              # Rust backend (DataChannel only, lightweight)
├── Lucid.Rtc.Rust.win-x64
├── Lucid.Rtc.Rust.win-x86
├── Lucid.Rtc.Rust.win-arm64
├── Lucid.Rtc.Rust.linux-x64
├── Lucid.Rtc.Rust.linux-arm64
├── Lucid.Rtc.Rust.linux-arm
├── Lucid.Rtc.Rust.osx-x64
├── Lucid.Rtc.Rust.osx-arm64
└── Lucid.Rtc.Rust.All      # All platforms in one package

Lucid.Rtc.Pion              # Pion/Go backend (audio/video ready)
├── Lucid.Rtc.Pion.win-x64
├── Lucid.Rtc.Pion.win-x86
├── Lucid.Rtc.Pion.win-arm64
├── Lucid.Rtc.Pion.linux-x64
├── Lucid.Rtc.Pion.linux-arm64
├── Lucid.Rtc.Pion.linux-arm
├── Lucid.Rtc.Pion.osx-x64
├── Lucid.Rtc.Pion.osx-arm64
└── Lucid.Rtc.Pion.All      # All platforms in one package

Lucid.Rtc                   # Metapackage (Core + Rust all platforms)
```

## Installation

### Rust Backend (DataChannel only, lightweight)

```xml
<!-- All platforms -->
<PackageReference Include="Lucid.Rtc" Version="0.1.0" />

<!-- Or specific platform -->
<PackageReference Include="Lucid.Rtc.Core" Version="0.1.0" />
<PackageReference Include="Lucid.Rtc.Rust.win-x64" Version="0.1.0" />
```

### Pion Backend (Audio/Video ready)

```xml
<PackageReference Include="Lucid.Rtc.Core" Version="0.1.0" />
<PackageReference Include="Lucid.Rtc.Pion.win-x64" Version="0.1.0" />
```

## Project Structure

```
Lucid.Rtc/
├── crates/
│   ├── lucid-rtc/              # Core Rust library
│   └── lucid-rtc-sys/          # FFI bindings (C ABI)
├── pion/
│   ├── go.mod
│   ├── client.go               # Go WebRTC client
│   └── exports.go              # C ABI exports
├── dotnet/
│   ├── Lucid.Rtc.Core/         # Core C# library
│   ├── Lucid.Rtc.Rust/         # Rust native packages
│   ├── Lucid.Rtc.Pion/         # Pion native packages
│   ├── Lucid.Rtc/              # Metapackage
│   └── Lucid.Rtc.Tests/        # Unit tests
├── scripts/
│   ├── build-pion.ps1          # Build Pion (Windows)
│   └── build-pion.sh           # Build Pion (Unix)
├── build.ps1                   # Build Rust (Windows)
├── build.sh                    # Build Rust (Unix)
└── .github/workflows/
    └── build.yml               # CI/CD pipeline
```

## Building

### Prerequisites

- Rust (stable) - for Rust backend
- Go 1.21+ - for Pion backend
- .NET 10.0 SDK
- GCC/MinGW (for CGO on Windows)

### Build Rust Backend

```powershell
# Windows
./build.ps1 -Target x86_64-pc-windows-msvc -Pack

# Linux/macOS
./build.sh -t x86_64-unknown-linux-gnu -p
```

### Build Pion Backend

```powershell
# Windows
./scripts/build-pion.ps1 -Target win-x64

# Linux/macOS
./scripts/build-pion.sh -t linux/amd64
```

### Build .NET Solution

```bash
dotnet build Lucid.Rtc.slnx
dotnet test Lucid.Rtc.slnx
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
            Console.WriteLine($"Message from {evt.PeerId}: {Encoding.UTF8.GetString(evt.Message!)}");
            break;

        case "ice_candidate":
            // Send ICE candidate to signaling server
            signalingServer.SendIceCandidate(evt.PeerId, evt.Candidate!);
            break;

        case "data_channel_open":
            Console.WriteLine($"Peer connected: {evt.PeerId}");
            break;
    }
}
```

### Complete P2P Flow

```csharp
// Offerer
var offer = client.CreateOffer("peer1");
await signalingServer.SendOfferAsync("peer1", offer);
// Wait for answer, then set it
client.SetRemoteAnswer("peer1", answer);

// Answerer
var answer = client.SetRemoteOffer("peer1", offer);
await signalingServer.SendAnswerAsync("peer1", answer);

// Both: exchange ICE candidates
client.AddIceCandidate("peer1", candidate);
```

## API Reference

### Check Backend

```csharp
// Check which backend is loaded
if (MediaClient.IsPionBackend)
{
    Console.WriteLine("Using Pion backend - media features available");
}
else
{
    Console.WriteLine("Using Rust backend - DataChannel only");
}
```

### Media Features (Pion Only)

```csharp
// Get supported codecs
var codecs = MediaClient.GetSupportedCodecs();
foreach (var codec in codecs)
{
    Console.WriteLine($"{codec.MimeType} @ {codec.ClockRate}Hz");
}

// Create media client
using var client = new MediaClient(config);

// Create audio track (Opus)
var audioTrackId = client.CreateMediaTrack(new MediaTrackConfig
{
    Kind = "audio",
    Codec = "opus"
});

// Create video track (VP8)
var videoTrackId = client.CreateMediaTrack(new MediaTrackConfig
{
    Kind = "video",
    Codec = "vp8"
});

// Add tracks to peer
client.AddTrackToPeer("peer1", audioTrackId);
client.AddTrackToPeer("peer1", videoTrackId);

// Send RTP data (you need to encode to RTP packets)
client.SendMediaData(audioTrackId, rtpPacketData);
```

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
| RtcClient(config) | Create a new client |
| CreateOffer(peerId) | Create SDP offer |
| SetRemoteOffer(peerId, sdp) | Set remote offer, get answer |
| SetRemoteAnswer(peerId, sdp) | Set remote answer |
| AddIceCandidate(...) | Add ICE candidate |
| SendMessage(peerId, data) | Send binary data |
| Broadcast(data) | Send to all peers |
| IsConnected(peerId) | Check connection state |
| ClosePeer(peerId) | Close peer connection |
| CloseAllPeers() | Close all connections |

### RtcEvent Types

| Type | Description |
|------|-------------|
| Connected | Peer connected |
| Disconnected | Peer disconnected |
| Message | Data received |
| IceCandidate | ICE candidate generated |
| Error | Error occurred |

## Go Environment Setup (for Pion Backend)

### Windows

1. **Install Go**:
   ```powershell
   # Using Chocolatey
   choco install golang

   # Or download from https://go.dev/dl/
   ```

2. **Install MinGW (for CGO)**:
   ```powershell
   choco install mingw
   ```

3. **Verify installation**:
   ```powershell
   go version
   gcc --version
   ```

4. **Build Pion**:
   ```powershell
   ./scripts/build-pion.ps1 -Target win-x64
   ```

### Linux

```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install -y golang gcc

# Fedora
sudo dnf install -y golang gcc

# Arch
sudo pacman -S go gcc

# Build
./scripts/build-pion.sh -t linux/amd64
```

### macOS

```bash
# Using Homebrew
brew install go

# Xcode Command Line Tools (for clang)
xcode-select --install

# Build
./scripts/build-pion.sh -t darwin/arm64  # Apple Silicon
./scripts/build-pion.sh -t darwin/amd64  # Intel
```

### Cross-Compilation

For cross-compiling to other platforms, you need cross-compilers:

```bash
# Linux ARM64
sudo apt-get install -y gcc-aarch64-linux-gnu
CC=aarch64-linux-gnu-gcc ./scripts/build-pion.sh -t linux/arm64

# Linux ARM
sudo apt-get install -y gcc-arm-linux-gnueabihf
CC=arm-linux-gnueabihf-gcc ./scripts/build-pion.sh -t linux/arm
```

## License

MIT
