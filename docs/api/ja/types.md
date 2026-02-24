# 共通型

[English](../types.md) | **日本語**

[← README](../../../README-ja.md) | [High-Level](high-level.md) | [Low-Level](low-level.md) | [型](types.md) | [MessagePack](messagepack.md)

---

## IceCandidate

ICE候補情報。

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Candidate` | `string` | 候補文字列 |
| `SdpMid` | `string` | SDPメディアID |
| `SdpMlineIndex` | `int` | SDPメディアラインインデックス |

### 例

```csharp
var candidate = new IceCandidate
{
    Candidate = "candidate:1 1 UDP 2122260223 192.168.1.1 54321 typ host",
    SdpMid = "0",
    SdpMlineIndex = 0
};

// High-Level API
peer.AddIceCandidate(candidate);

// Low-Level API
client.AddIceCandidate("peer1", candidate);
```

---

## MediaCodec

メディアコーデック情報（Pionバックエンド）。

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `MimeType` | `string` | MIMEタイプ（例: "audio/opus", "video/VP8"） |
| `ClockRate` | `uint` | クロックレート（Hz） |
| `Channels` | `ushort` | オーディオチャンネル数 |
| `SdpFmtpLine` | `string?` | SDPフォーマットパラメータ |

### 例

```csharp
var codecs = MediaClient.GetSupportedCodecs();
foreach (var codec in codecs)
{
    Console.WriteLine($"{codec.MimeType} @ {codec.ClockRate}Hz");
}
// 出力:
// audio/opus @ 48000Hz
// video/VP8 @ 90000Hz
// video/VP9 @ 90000Hz
// video/H264 @ 90000Hz
```

---

## MediaTrackConfig

メディアトラック作成用設定。

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Kind` | `string` | "audio" または "video" |
| `Codec` | `string` | コーデック名 |
| `TrackId` | `string?` | カスタムトラックID（任意） |
| `StreamId` | `string?` | カスタムストリームID（任意） |

### サポートコーデック

| 種類 | コーデック | 説明 |
|------|----------|------|
| audio | `opus` | Opusコーデック（推奨） |
| audio | `pcmu` | G.711 μ-law |
| audio | `pcma` | G.711 A-law |
| video | `vp8` | VP8（推奨） |
| video | `vp9` | VP9 |
| video | `h264` | H.264 |
| video | `av1` | AV1 |

### 例

```csharp
// ビデオトラック
var videoConfig = new MediaTrackConfig
{
    Kind = "video",
    Codec = "vp8",
    TrackId = "video-0",
    StreamId = "stream-0"
};

// オーディオトラック
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

低レベル`RtcClient`用設定。

### プロパティ

| プロパティ | 型 | デフォルト | 説明 |
|-----------|-----|-----------|------|
| `StunServers` | `string[]?` | `["stun:stun.l.google.com:19302"]` | STUNサーバーURL |
| `TurnServerUrl` | `string?` | `null` | TURNサーバーURL |
| `TurnUsername` | `string?` | `null` | TURNユーザー名 |
| `TurnPassword` | `string?` | `null` | TURNパスワード |
| `IceConnectionTimeoutMs` | `int` | `30000` | ICEタイムアウト（ms） |
| `DataChannelReliable` | `bool` | `true` | DataChannel信頼性 |

### 例

```csharp
// 基本設定
var config = new RtcConfig
{
    StunServers = new[] { "stun:stun.l.google.com:19302" }
};

// TURNサーバー付き
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
