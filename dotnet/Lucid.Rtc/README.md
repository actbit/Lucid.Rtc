# Lucid.Rtc

WebRTC bindings for .NET with native P2P communication support.

## Installation

### For most users (recommended)

```xml
<PackageReference Include="Lucid.Rtc" Version="0.1.0" />
```

This includes everything you need - managed library and native runtimes for all platforms.

### For advanced users

If you want to minimize package size, install only the packages you need:

```xml
<!-- Managed library only -->
<PackageReference Include="Lucid.Rtc.Core" Version="0.1.0" />

<!-- Plus the native runtime for your platform -->
<PackageReference Include="Lucid.Rtc.Native.win-x64" Version="0.1.0" />
```

## Available Packages

| Package | Description | Size |
|---------|-------------|------|
| `Lucid.Rtc` | **Metapackage** - Everything included | ~200 MB |
| `Lucid.Rtc.Core` | Managed library only | ~50 KB |
| `Lucid.Rtc.Native.All` | All native runtimes | ~200 MB |
| `Lucid.Rtc.Native.win-x64` | Windows x64 | ~20 MB |
| `Lucid.Rtc.Native.win-arm64` | Windows ARM64 | ~20 MB |
| `Lucid.Rtc.Native.win-x86` | Windows x86 | ~20 MB |
| `Lucid.Rtc.Native.linux-x64` | Linux x64 | ~20 MB |
| `Lucid.Rtc.Native.linux-arm64` | Linux ARM64 | ~20 MB |
| `Lucid.Rtc.Native.linux-arm` | Linux ARM (32-bit) | ~20 MB |
| `Lucid.Rtc.Native.osx-x64` | macOS Intel | ~20 MB |
| `Lucid.Rtc.Native.osx-arm64` | macOS Apple Silicon | ~20 MB |

## Quick Start

```csharp
using Lucid.Rtc;

var config = new RtcConfig
{
    StunServers = new[] { "stun:stun.l.google.com:19302" }
};

using var client = new RtcClient(config);

// Create offer
var offer = client.CreateOffer("peer1");
Console.WriteLine($"Offer: {offer}");

// Handle events
client.EventReceived += (sender, evt) =>
{
    Console.WriteLine($"Event: {evt.Type}");
};
```

## License

MIT
