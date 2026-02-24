# MessagePack拡張

[English](../messagepack.md) | **日本語**

[← README](../../../README-ja.md) | [High-Level](high-level.md) | [Low-Level](low-level.md) | [型](types.md) | [MessagePack](messagepack.md)

---

型安全なオブジェクトシリアライズ用オプションパッケージ。

## インストール

```xml
<PackageReference Include="Lucid.Rtc.MessagePack" Version="0.1.0" />
```

---

## RtcMessagePackExtensions

拡張メソッドを提供するスタティッククラス。

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Options` | `MessagePackSerializerOptions` | シリアライザオプション |

### 拡張メソッド

| メソッド | 対象 | パラメータ | 戻り値 | 説明 |
|---------|------|-----------|--------|------|
| `SendObject<T>` | `Peer` | `T value` | `void` | オブジェクト送信 |
| `BroadcastObject<T>` | `RtcConnection` | `T value` | `void` | オブジェクトブロードキャスト |
| `OnObject<T>` | `RtcConnection` | `Action<ObjectReceivedEvent<T>>` | `RtcConnection` | ハンドラ登録 |

---

## ObjectReceivedEvent<T>

受信したMessagePackオブジェクト用イベント。

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `PeerId` | `string?` | 送信元ピアID |
| `Peer` | `Peer?` | 送信元ピアオブジェクト |
| `Value` | `T` | デシリアライズ済みオブジェクト |
| `RawData` | `byte[]?` | 生MessagePackデータ |

---

## 使い方

### メッセージ型定義

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

### オブジェクト送信

```csharp
// 特定のピアへ
peer.SendObject(new ChatMessage
{
    User = "Alice",
    Text = "こんにちは！"
});

// 全ピアにブロードキャスト
connection.BroadcastObject(new ChatMessage
{
    User = "System",
    Text = "ようこそ！"
});

// ゲーム状態更新
peer.SendObject(new PositionUpdate { X = 100, Y = 50, Z = 0 });
```

### オブジェクト受信

```csharp
// 型付きハンドラ登録
connection.OnObject<ChatMessage>(e =>
{
    Console.WriteLine($"[{e.Value.User}] {e.Value.Text}");
});

connection.OnObject<PositionUpdate>(e =>
{
    UpdatePlayerPosition(e.PeerId, e.Value.X, e.Value.Y, e.Value.Z);
});
```

### シリアライズ設定

```csharp
// LZ4圧縮（大きなオブジェクト向け）
RtcMessagePackExtensions.Options = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.Lz4Block);

// カスタムリゾルバ
RtcMessagePackExtensions.Options = MessagePackSerializerOptions.Standard
    .WithResolver(CompositeResolver.Create(
        NativeDecimalResolver.Instance,
        StandardResolver.Instance
    ));
```

---

## 完全例

```csharp
using Lucid.Rtc;
using MessagePack;

// セットアップ
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .Build();

// 圧縮有効化（任意）
RtcMessagePackExtensions.Options = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.Lz4Block);

// ハンドラ登録
connection
    .OnObject<ChatMessage>(e =>
    {
        Console.WriteLine($"[{e.Value.User}] {e.Value.Text}");
    })
    .OnObject<PositionUpdate>(e =>
    {
        Console.WriteLine($"位置: ({e.Value.X}, {e.Value.Y}, {e.Value.Z})");
    });

// ピア作成
var peer = await connection.CreatePeerAsync("remote-player");

// ... SDPネゴシエーション ...

// 型付きメッセージ送信
peer.SendObject(new ChatMessage { User = "Player1", Text = "こんにちは！" });
peer.SendObject(new PositionUpdate { X = 10.5f, Y = 20.0f, Z = 5.0f });

// ブロードキャスト
connection.BroadcastObject(new ChatMessage { User = "Server", Text = "ゲーム開始！" });
```

---

## MessagePack属性

### [MessagePackObject]

クラスまたは構造体をシリアライズ可能としてマーク。

```csharp
[MessagePackObject]
public class MyClass
{
    // ...
}
```

### [Key(int)]

インデックスによるプロパティキー（パフォーマンス推奨）。

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

文字列名によるプロパティキー。

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

シリアライズからプロパティを除外。

```csharp
[MessagePackObject]
public class User
{
    [Key(0)]
    public string Name { get; set; } = "";

    [IgnoreMember]
    public string Password { get; set; } = "";  // シリアライズされない
}
```
