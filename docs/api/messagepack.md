# MessagePack Extensions

Optional package for strongly-typed object serialization.

## Installation

```xml
<PackageReference Include="Lucid.Rtc.MessagePack" Version="0.1.0" />
```

---

## RtcMessagePackExtensions

Static class providing extension methods.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Options` | `MessagePackSerializerOptions` | Serializer options |

### Extension Methods

| Method | Target | Parameters | Returns | Description |
|--------|--------|------------|---------|-------------|
| `SendObject<T>` | `Peer` | `T value` | `void` | Send object |
| `BroadcastObject<T>` | `RtcConnection` | `T value` | `void` | Broadcast object |
| `OnObject<T>` | `RtcConnection` | `Action<ObjectReceivedEvent<T>>` | `RtcConnection` | Register handler |

---

## ObjectReceivedEvent<T>

Event for received MessagePack objects.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `PeerId` | `string?` | Source peer ID |
| `Peer` | `Peer?` | Source peer object |
| `Value` | `T` | Deserialized object |
| `RawData` | `byte[]?` | Raw MessagePack data |

---

## Usage

### Define Message Types

```csharp
using MessagePack;

[MessagePackObject]
public class ChatMessage
{
    [Key(0)]
    public string User { get; set; } = "";

    [Key(1)]
    public string Text { get; set; } = "";
}

[MessagePackObject]
public class PositionUpdate
{
    [Key(0)]
    public float X { get; set; }

    [Key(1)]
    public float Y { get; set; }

    [Key(2)]
    public float Z { get; set; }
}
```

### Send Objects

```csharp
// To specific peer
peer.SendObject(new ChatMessage
{
    User = "Alice",
    Text = "Hello World!"
});

// Broadcast to all peers
connection.BroadcastObject(new ChatMessage
{
    User = "System",
    Text = "Welcome!"
});

// Game state update
peer.SendObject(new PositionUpdate { X = 100, Y = 50, Z = 0 });
```

### Receive Objects

```csharp
// Register typed handlers
connection.OnObject<ChatMessage>(e =>
{
    Console.WriteLine($"[{e.Value.User}] {e.Value.Text}");
});

connection.OnObject<PositionUpdate>(e =>
{
    UpdatePlayerPosition(e.PeerId, e.Value.X, e.Value.Y, e.Value.Z);
});
```

### Configure Serialization

```csharp
// LZ4 compression (recommended for large objects)
RtcMessagePackExtensions.Options = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.Lz4Block);

// Custom resolver
RtcMessagePackExtensions.Options = MessagePackSerializerOptions.Standard
    .WithResolver(CompositeResolver.Create(
        NativeDecimalResolver.Instance,
        StandardResolver.Instance
    ));
```

---

## Complete Example

```csharp
using Lucid.Rtc;
using MessagePack;

// Setup
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .Build();

// Enable compression (optional)
RtcMessagePackExtensions.Options = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.Lz4Block);

// Register handlers
connection
    .OnObject<ChatMessage>(e =>
    {
        Console.WriteLine($"[{e.Value.User}] {e.Value.Text}");
    })
    .OnObject<PositionUpdate>(e =>
    {
        Console.WriteLine($"Position: ({e.Value.X}, {e.Value.Y}, {e.Value.Z})");
    });

// Create peer
var peer = await connection.CreatePeerAsync("remote-player");

// ... SDP negotiation ...

// Send typed messages
peer.SendObject(new ChatMessage { User = "Player1", Text = "Hello!" });
peer.SendObject(new PositionUpdate { X = 10.5f, Y = 20.0f, Z = 5.0f });

// Broadcast
connection.BroadcastObject(new ChatMessage { User = "Server", Text = "Game starting!" });
```

---

## MessagePack Attributes

### [MessagePackObject]

Marks a class or struct as serializable.

```csharp
[MessagePackObject]
public class MyClass
{
    // ...
}
```

### [Key(int)]

Property key by index (recommended for performance).

```csharp
[MessagePackObject]
public class Player
{
    [Key(0)]
    public int Id { get; set; }

    [Key(1)]
    public string Name { get; set; } = "";
}
```

### [Key(string)]

Property key by string name.

```csharp
[MessagePackObject(keyAsString: true)]
public class Config
{
    [Key("maxPlayers")]
    public int MaxPlayers { get; set; }

    [Key("serverName")]
    public string ServerName { get; set; } = "";
}
```

### [IgnoreMember]

Exclude property from serialization.

```csharp
[MessagePackObject]
public class User
{
    [Key(0)]
    public string Name { get; set; } = "";

    [IgnoreMember]
    public string Password { get; set; } = "";  // Not serialized
}
```
