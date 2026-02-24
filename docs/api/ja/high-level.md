# High-Level API

[English](../high-level.md) | **日本語**

[← README](../../../README-ja.md) | [High-Level](high-level.md) | [Low-Level](low-level.md) | [型](types.md) | [MessagePack](messagepack.md)

---

ほとんどのユースケース向けのモダンなフルエントAPI。

## RtcConnectionBuilder

`RtcConnection`インスタンスを作成するためのビルダー。

### メソッド

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `WithStunServer(string)` | `stunServer` - STUNサーバーURL | `this` | STUNサーバーを追加 |
| `WithStunServers(params string[])` | `stunServers` - URL配列 | `this` | 複数のSTUNサーバーを追加 |
| `WithTurnServer(string, string, string)` | `url`, `username`, `password` | `this` | TURNサーバーを設定 |
| `WithIceConnectionTimeout(int)` | `timeoutMs` (デフォルト: 30000) | `this` | ICE接続タイムアウトを設定 |
| `WithDataChannelReliable(bool)` | `reliable` (デフォルト: true) | `this` | DataChannelの信頼性を設定 |
| `Build()` | - | `RtcConnection` | 接続インスタンスを作成 |

### 例

```csharp
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .WithTurnServer("turn:example.com:3478", "user", "pass")
    .Build();
```

---

## RtcConnection

WebRTC通信用のメイン接続クラス。

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `IsConnected` | `bool` | ネイティブ接続が利用可能か |
| `Version` | `string` (static) | ライブラリバージョン |

### メソッド

#### イベント登録

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `On<T>(Action<T>)` | `handler` | `this` | 型付きイベントハンドラを登録 |
| `Off<T>()` | - | `this` | 型Tの全ハンドラを削除 |

#### ピア管理

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `CreatePeerAsync(string)` | `peerId` | `Task<Peer>` | 新しいピアを作成 |
| `GetPeer(string)` | `peerId` | `Peer?` | 既存のピアを取得 |
| `GetConnectedPeers()` | - | `IEnumerable<Peer>` | 接続中の全ピアを取得 |

#### メッセージング

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `Broadcast(string)` | `message` | `void` | 全ピアに文字列をブロードキャスト |
| `Broadcast(byte[])` | `data` | `void` | 全ピアにバイナリをブロードキャスト |

#### ライフサイクル

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `DisposeAsync()` | - | `ValueTask` | 接続を破棄 |

### 例

```csharp
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .Build();

connection
    .On<PeerConnectedEvent>(e => Console.WriteLine($"接続: {e.PeerId}"))
    .On<MessageReceivedEvent>(e => Console.WriteLine($"メッセージ: {e.DataAsString}"))
    .On<IceCandidateEvent>(e => SendToSignaling(e.Candidate));

var peer = await connection.CreatePeerAsync("remote-peer");
connection.Broadcast("こんにちは！");

await connection.DisposeAsync();
```

---

## Peer

WebRTCピア接続を表します。

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Id` | `string` | ピア識別子 |
| `IsConnected` | `bool` | 接続状態 |
| `State` | `PeerState` | 接続状態 |
| `IsVideoEnabled` | `bool` | ビデオ有効 |
| `IsAudioEnabled` | `bool` | オーディオ有効 |

### メソッド

#### 設定（チェーン可）

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `WithVideo(VideoCodec)` | `codec` - `VideoCodec.Vp8`, `Vp9`, `H264`, `Av1` | `this` | ビデオを有効化 (列挙型) |
| `WithVideo(string)` | `codec` - "vp8", "vp9", "h264", "av1" | `this` | ビデオを有効化 (文字列) |
| `WithAudio(AudioCodec)` | `codec` - `AudioCodec.Opus`, `Pcmu`, `Pcma` | `this` | オーディオを有効化 (列挙型) |
| `WithAudio(string)` | `codec` - "opus", "pcmu", "pcma" | `this` | オーディオを有効化 (文字列) |

#### SDPネゴシエーション（チェーン可）

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `SetRemoteOffer(string)` | `sdp` | `this` | リモートオファーを設定 |
| `SetRemoteAnswer(string)` | `sdp` | `this` | リモートアンサーを設定 |
| `AddIceCandidate(IceCandidate)` | `candidate` | `this` | ICE候補を追加 |

#### メッセージング

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `Send(string)` | `message` | `void` | 文字列を送信 |
| `Send(byte[])` | `data` | `void` | バイナリを送信 |
| `SendVideo(byte[])` | `rtpData` | `void` | ビデオRTP送信 (Pion) |
| `SendAudio(byte[])` | `rtpData` | `void` | オーディオRTP送信 (Pion) |

#### ライフサイクル

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `CloseAsync()` | - | `Task` | ピア接続をクローズ |

### 例

```csharp
// 列挙型を使用 (推奨)
var peer = await connection.CreatePeerAsync("remote-peer")
    .WithVideo(VideoCodec.Vp8)
    .WithAudio(AudioCodec.Opus);

// または文字列で指定
var peer = await connection.CreatePeerAsync("remote-peer")
    .WithVideo("vp8")
    .WithAudio("opus");

peer.SetRemoteOffer(remoteOfferSdp);
peer.AddIceCandidate(candidate);

peer.Send("こんにちは！");
peer.SendVideo(rtpPacket);

await peer.CloseAsync();
```

---

## VideoCodec

| 値 | 説明 |
|----|------|
| `Vp8` | VP8コーデック (推奨) |
| `Vp9` | VP9コーデック |
| `H264` | H.264コーデック |
| `Av1` | AV1コーデック |

---

## AudioCodec

| 値 | 説明 |
|----|------|
| `Opus` | Opusコーデック (推奨) |
| `Pcmu` | G.711 μ-law |
| `Pcma` | G.711 A-law |

---

## PeerState

| 値 | 説明 |
|----|------|
| `Connecting` | 接続確立中 |
| `Connected` | データ送受信可能 |
| `Disconnected` | 切断済み |
| `Failed` | 接続失敗 |
| `Closed` | クローズ済み |

---

## イベント

すべてのイベントは`RtcEventBase`を継承します。

### RtcEventBase

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `PeerId` | `string?` | ピアID |
| `Peer` | `Peer?` | ピアオブジェクト |

### イベント種類

| イベント | プロパティ | 説明 |
|---------|-----------|------|
| `PeerConnectedEvent` | - | ピア接続 |
| `PeerDisconnectedEvent` | - | ピア切断 |
| `MessageReceivedEvent` | `Data`, `DataAsString` | データ受信 |
| `IceCandidateEvent` | `Candidate` | ICE候補準備完了 |
| `OfferReadyEvent` | `Sdp` | SDPオファー準備完了 |
| `AnswerReadyEvent` | `Sdp` | SDPアンサー準備完了 |
| `IceConnectionStateChangeEvent` | `State` | ICE状態変更 |
| `DataChannelOpenEvent` | - | DataChannelオープン |
| `DataChannelClosedEvent` | - | DataChannelクローズ |
| `VideoFrameEvent` | `Data` | ビデオフレーム (Pion) |
| `AudioFrameEvent` | `Data` | オーディオフレーム (Pion) |
| `ErrorEvent` | `Message` | エラー発生 |

### 例

```csharp
connection
    .On<PeerConnectedEvent>(e => Console.WriteLine($"接続: {e.PeerId}"))
    .On<MessageReceivedEvent>(e => Console.WriteLine($"{e.PeerId}: {e.DataAsString}"))
    .On<IceCandidateEvent>(e => signaling.Send(e.Candidate))
    .On<VideoFrameEvent>(e => DisplayFrame(e.Data))
    .On<ErrorEvent>(e => Console.WriteLine($"エラー: {e.Message}"));
```
