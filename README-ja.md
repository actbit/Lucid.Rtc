# Lucid.Rtc

[English](./README.md) | **日本語**

**.NET向けWebRTC** - クロスプラットフォーム、マルチバックエンド対応のモダンなフルエントAPI。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![NuGet](https://img.shields.io/nuget/v/Lucid.Rtc.Core.svg)](https://www.nuget.org/packages/Lucid.Rtc.Core/)

## 特徴

- 🚀 **モダンなフルエントAPI** - SignalRインスパイア、メソッドチェーン対応
- 🌍 **クロスプラットフォーム** - Windows、Linux、macOS (x64, ARM64, ARM)
- 🔧 **マルチバックエンド** - Rust (軽量) と Pion/Go (メディア対応)
- 📦 **モジュラーパッケージ** - 必要なものだけ使用
- 🔄 **MessagePack対応** - オプションのシリアライズパッケージ
- 📡 **DataChannel** - 信頼性のあるP2Pメッセージング
- 🎥 **メディアサポート** - オーディオ/ビデオ (Pionバックエンドのみ)

## クイックスタート

### インストール

```xml
<!-- コアライブラリ + Rustバックエンド (DataChannel向け推奨) -->
<PackageReference Include="Lucid.Rtc" Version="0.1.0" />

<!-- または メディアサポート付き (Pionバックエンド) -->
<PackageReference Include="Lucid.Rtc.Core" Version="0.1.0" />
<PackageReference Include="Lucid.Rtc.Pion.win-x64" Version="0.1.0" />

<!-- オプション: MessagePackシリアライズ -->
<PackageReference Include="Lucid.Rtc.MessagePack" Version="0.1.0" />
```

### 基本的な使い方

```csharp
using Lucid.Rtc;

// 接続作成
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .Build();

// イベントハンドラ登録 (メソッドチェーン)
connection
    .On<PeerConnectedEvent>(e => Console.WriteLine($"接続: {e.PeerId}"))
    .On<MessageReceivedEvent>(e => Console.WriteLine($"メッセージ: {e.DataAsString}"))
    .On<IceCandidateEvent>(e => SendToSignaling(e.Candidate));

// メディアサポート付きピア作成 (列挙型を使用)
var peer = await connection.CreatePeerAsync("remote-peer")
    .WithVideo(VideoCodec.Vp8)
    .WithAudio(AudioCodec.Opus);

// ネゴシエーション
peer.SetRemoteOffer(offerSdp);

// データ送信
peer.Send("こんにちは！");
peer.SendVideo(rtpData);

// クリーンアップ
await peer.CloseAsync();
await connection.DisposeAsync();
```

---

## APIドキュメント

- [High-Level API](./docs/api/ja/high-level.md) - RtcConnection、Peer、イベント
- [Low-Level API](./docs/api/ja/low-level.md) - RtcClient、MediaClient
- [共通型](./docs/api/ja/types.md) - IceCandidate、MediaCodec等
- [MessagePack拡張](./docs/api/ja/messagepack.md) - オブジェクトシリアライズ

### サンプルプロジェクト

- [HighLevelSample](./dotnet/samples/HighLevelSample/) - フルエントAPIのサンプル
- [LowLevelSample](./dotnet/samples/LowLevelSample/) - 低レベルAPIのサンプル

---

## RtcConnectionBuilder

接続作成用のフルエントビルダー:

```csharp
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .WithStunServer("stun:stun1.l.google.com:19302")  // 複数サーバー
    .WithTurnServer("turn:example.com:3478", "user", "pass")
    .WithIceConnectionTimeout(30000)
    .WithDataChannelReliable(true)
    .Build();
```

## RtcConnection

イベント処理付きメイン接続クラス:

```csharp
// イベント (メソッドチェーン対応)
connection
    .On<PeerConnectedEvent>(e => { })
    .On<PeerDisconnectedEvent>(e => { })
    .On<MessageReceivedEvent>(e => { })
    .On<IceCandidateEvent>(e => { })
    .On<OfferReadyEvent>(e => { })
    .On<AnswerReadyEvent>(e => { })
    .On<DataChannelOpenEvent>(e => { })
    .On<DataChannelClosedEvent>(e => { })
    .On<VideoFrameEvent>(e => { })      // Pionのみ
    .On<AudioFrameEvent>(e => { })      // Pionのみ
    .On<ErrorEvent>(e => { });

// ピア管理
var peer = await connection.CreatePeerAsync("peer-id");
var existingPeer = connection.GetPeer("peer-id");
var allPeers = connection.GetConnectedPeers();

// ブロードキャスト
connection.Broadcast("みなさんこんにちは！");
connection.Broadcast(binaryData);
```

## Peer

ピア接続を表します:

```csharp
// プロパティ
peer.Id              // "peer-id"
peer.IsConnected     // true/false
peer.State           // Connecting, Connected, Disconnected, Failed, Closed
peer.IsVideoEnabled  // true/false
peer.IsAudioEnabled  // true/false

// 設定 (メソッドチェーン対応)
peer.WithVideo(VideoCodec.Vp8)   // または "vp8", "vp9", "h264", "av1"
peer.WithAudio(AudioCodec.Opus)  // または "opus", "pcmu", "pcma"

// SDPネゴシエーション (メソッドチェーン対応)
peer.SetRemoteOffer(sdp)
peer.SetRemoteAnswer(sdp)
peer.AddIceCandidate(candidate)

// データ送信 (チェーンなし - 送信のみ)
peer.Send("テキストメッセージ");
peer.Send(binaryData);
peer.SendVideo(rtpPacket);   // Pionのみ
peer.SendAudio(rtpPacket);   // Pionのみ

// クローズ
await peer.CloseAsync();
```

## イベント

| イベント | 説明 | プロパティ |
|---------|------|-----------|
| `PeerConnectedEvent` | ピア接続 | `PeerId`, `Peer` |
| `PeerDisconnectedEvent` | ピア切断 | `PeerId`, `Peer` |
| `MessageReceivedEvent` | データ受信 | `PeerId`, `Peer`, `Data`, `DataAsString` |
| `IceCandidateEvent` | ICE候補準備完了 | `PeerId`, `Peer`, `Candidate` |
| `OfferReadyEvent` | SDPオファー準備完了 | `PeerId`, `Peer`, `Sdp` |
| `AnswerReadyEvent` | SDPアンサー準備完了 | `PeerId`, `Peer`, `Sdp` |
| `DataChannelOpenEvent` | DataChannelオープン | `PeerId`, `Peer` |
| `DataChannelClosedEvent` | DataChannelクローズ | `PeerId`, `Peer` |
| `VideoFrameEvent` | ビデオフレーム受信 | `PeerId`, `Peer`, `Data` |
| `AudioFrameEvent` | オーディオフレーム受信 | `PeerId`, `Peer`, `Data` |
| `ErrorEvent` | エラー発生 | `Message` |

---

## MessagePackシリアライズ

型安全なオブジェクトシリアライズ用オプションパッケージ:

```xml
<PackageReference Include="Lucid.Rtc.MessagePack" Version="0.1.0" />
```

```csharp
using Lucid.Rtc;

// メッセージ型定義
[MessagePackObject]
public class ChatMessage
{
    [Key(0)] public string User { get; set; } = "";
    [Key(1)] public string Text { get; set; } = "";
}

// オブジェクト送信
peer.SendObject(new ChatMessage { User = "Alice", Text = "こんにちは！" });
connection.BroadcastObject(new ChatMessage { User = "System", Text = "ようこそ！" });

// オブジェクト受信
connection.OnObject<ChatMessage>(e =>
{
    Console.WriteLine($"{e.Value.User}: {e.Value.Text}");
});

// オプション: 圧縮設定
RtcMessagePackExtensions.Options = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.Lz4Block);
```

---

## バックエンド比較

| 機能 | Rust (webrtc-rs) | Pion (Go) |
|------|------------------|-----------|
| DataChannel | ✅ | ✅ |
| オーディオコーデック | ❌ | Opus, G722, PCMU, PCMA |
| ビデオコーデック | ❌ | VP8, VP9, H264, AV1 |
| Simulcast | ❌ | ✅ |
| バイナリサイズ | ~5MB | ~18MB |
| 成熟度 | 実験的 | 本番対応 |

**推奨**: DataChannelのみのアプリは **Rust**。オーディオ/ビデオが必要な場合は **Pion**。

---

## パッケージ構成

```
Lucid.Rtc                    # メタパッケージ (Core + Rust全プラットフォーム)
├── Lucid.Rtc.Core           # コアライブラリ (必須)
├── Lucid.Rtc.MessagePack    # オプション: MessagePack対応

Lucid.Rtc.Rust               # Rustバックエンドパッケージ
├── Lucid.Rtc.Rust.win-x64
├── Lucid.Rtc.Rust.win-x86
├── Lucid.Rtc.Rust.win-arm64
├── Lucid.Rtc.Rust.linux-x64
├── Lucid.Rtc.Rust.linux-arm64
├── Lucid.Rtc.Rust.linux-arm
├── Lucid.Rtc.Rust.osx-x64
├── Lucid.Rtc.Rust.osx-arm64
└── Lucid.Rtc.Rust.All       # 全プラットフォーム

Lucid.Rtc.Pion               # Pionバックエンドパッケージ
├── Lucid.Rtc.Pion.win-x64
├── Lucid.Rtc.Pion.win-x86
├── Lucid.Rtc.Pion.win-arm64
├── Lucid.Rtc.Pion.linux-x64
├── Lucid.Rtc.Pion.linux-arm64
├── Lucid.Rtc.Pion.linux-arm
├── Lucid.Rtc.Pion.osx-x64
├── Lucid.Rtc.Pion.osx-arm64
└── Lucid.Rtc.Pion.All       # 全プラットフォーム
```

---

## 完全例: P2Pチャット

```csharp
using Lucid.Rtc;

// セットアップ
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .Build();

connection
    .On<PeerConnectedEvent>(e => Console.WriteLine($"[{e.PeerId}] 接続"))
    .On<PeerDisconnectedEvent>(e => Console.WriteLine($"[{e.PeerId}] 切断"))
    .On<MessageReceivedEvent>(e => Console.WriteLine($"[{e.PeerId}] {e.DataAsString}"))
    .On<IceCandidateEvent>(e => signaling.SendCandidate(e.PeerId, e.Candidate));

// オファー側
var peer = await connection.CreatePeerAsync("bob");
// peer.SetRemoteAnswer(answerFromSignaling);

// アンサー側
// var peer = connection.GetPeer("alice");
// peer.SetRemoteOffer(offerFromSignaling);

// メッセージ送信
while (true)
{
    var input = Console.ReadLine();
    if (input == "quit") break;
    peer.Send(input);
}

// クリーンアップ
await peer.CloseAsync();
await connection.DisposeAsync();
```

---

## ソースからのビルド

### 前提条件

- **.NET 10.0 SDK**
- **Rust (stable)** - Rustバックエンド用
- **Go 1.21+** + **GCC/MinGW** - Pionバックエンド用

### ビルドコマンド

```bash
# .NETソリューションのビルド
dotnet build

# テスト実行
dotnet test

# NuGetパッケージ作成
dotnet pack -c Release -o ./artifacts

# Rustバックエンドのビルド
./build.ps1 -Target x86_64-pc-windows-msvc -Pack  # Windows
./build.sh -t x86_64-unknown-linux-gnu -p         # Linux/macOS

# Pionバックエンドのビルド (Go + GCCが必要)
cd pion && CGO_ENABLED=1 go build -buildmode=c-shared -o lucid_rtc.dll .
```

---

## プロジェクト構成

```
Lucid.Rtc/
├── crates/
│   ├── lucid-rtc/              # Rust WebRTC実装 (webrtc-rs使用)
│   └── lucid-rtc-sys/          # FFIバインディング (C ABI)
├── pion/
│   ├── go.mod
│   ├── client.go               # Go WebRTCクライアント (pion/webrtc使用)
│   ├── exports.go              # C ABI エクスポート
│   └── media.go                # メディアトラック対応
├── dotnet/
│   ├── Lucid.Rtc.Core/         # コアC#ライブラリ
│   ├── Lucid.Rtc.MessagePack/  # MessagePack拡張
│   ├── Lucid.Rtc.Rust/         # Rustネイティブパッケージ
│   ├── Lucid.Rtc.Pion/         # Pionネイティブパッケージ
│   ├── Lucid.Rtc/              # メタパッケージ
│   ├── Lucid.Rtc.Tests/        # ユニットテスト
│   └── samples/                # サンプルプロジェクト
│       ├── HighLevelSample/
│       └── LowLevelSample/
├── docs/api/                   # APIドキュメント
│   ├── ja/                     # 日本語
│   ├── high-level.md
│   ├── low-level.md
│   ├── types.md
│   └── messagepack.md
├── build.ps1 / build.sh        # ビルドスクリプト
└── .github/workflows/          # CI/CD
```

---

## Low-Level API

きめ細かい制御が必要な場合は、低レベル`RtcClient` APIを使用:

```csharp
// Low-Level API (RtcClient)
var config = new RtcConfig
{
    StunServers = new[] { "stun:stun.l.google.com:19302" }
};

var client = new RtcClient(config);

// イベントポーリング
client.EventReceived += (s, e) =>
{
    switch (e.Type)
    {
        case "message_received":
            Console.WriteLine($"メッセージ: {Encoding.UTF8.GetString(e.Message!)}");
            break;
    }
};

// 手動ポーリング (代替手段)
while (client.TryGetEvent(out var evt))
{
    HandleEvent(evt);
}

// 同期操作
var offer = client.CreateOffer("peer1");
client.SetRemoteAnswer("peer1", answer);
client.SendMessage("peer1", data);
```

### API比較

| 機能 | High-Level (RtcConnection) | Low-Level (RtcClient) |
|------|---------------------------|----------------------|
| スタイル | フルエント、非同期 | クラシック、同期 |
| イベント | 型付き (`On<T>`) | 文字列ベース (`evt.Type`) |
| チェーン | ✅ 対応 | ❌ |
| メディア | 統合 | 別 `MediaClient` |
| 制御 | 抽象化 | きめ細かい |

---

## ライセンス

MIT License - [LICENSE.txt](LICENSE.txt) を参照
