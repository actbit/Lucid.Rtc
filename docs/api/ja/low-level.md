# Low-Level API

[English](../low-level.md) | **日本語**

[← README](../../../README-ja.md) | [High-Level](high-level.md) | [Low-Level](low-level.md) | [型](types.md) | [MessagePack](messagepack.md)

---

きめ細かい制御のための直接FFIラッパー。

## RtcConfig

`RtcClient`の設定。

### プロパティ

| プロパティ | 型 | デフォルト | 説明 |
|-----------|-----|-----------|------|
| `StunServers` | `string[]?` | Google STUN | STUNサーバーURL |
| `TurnServerUrl` | `string?` | `null` | TURNサーバーURL |
| `TurnUsername` | `string?` | `null` | TURNユーザー名 |
| `TurnPassword` | `string?` | `null` | TURNパスワード |
| `IceConnectionTimeoutMs` | `int` | 30000 | ICEタイムアウト (ms) |
| `DataChannelReliable` | `bool` | `true` | DataChannel信頼性 |

### 例

```csharp
var config = new RtcConfig
{
    StunServers = new[] { "stun:stun.l.google.com:19302" },
    TurnServerUrl = "turn:example.com:3478",
    TurnUsername = "user",
    TurnPassword = "pass"
};
```

---

## RtcClient

低レベルWebRTCクライアント。

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `IsNativeAvailable` | `bool` | ネイティブライブラリ読み込み済み |
| `EventReceived` | `event` | RTCイベントハンドラ |

### スタティックメソッド

| メソッド | 戻り値 | 説明 |
|---------|--------|------|
| `GetVersion()` | `string` | ライブラリバージョン |

### インスタンスメソッド

#### ライフサイクル

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `RtcClient(RtcConfig?)` | `config` | - | クライアント作成 |
| `Dispose()` | - | `void` | クライアント破棄 |

#### SDPネゴシエーション

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `CreateOffer(string)` | `peerId` | `string?` | SDPオファー作成 |
| `SetRemoteOffer(string, string)` | `peerId`, `sdp` | `string?` | オファー設定、アンサー取得 |
| `SetRemoteAnswer(string, string)` | `peerId`, `sdp` | `bool` | リモートアンサー設定 |

#### ICE

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `AddIceCandidate(string, string, string, int)` | `peerId`, `candidate`, `sdpMid`, `sdpMlineIndex` | `bool` | ICE候補追加 |
| `AddIceCandidate(string, IceCandidate)` | `peerId`, `candidate` | `bool` | ICE候補追加（オブジェクト） |

#### メッセージング

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `SendMessage(string, byte[])` | `peerId`, `data` | `bool` | バイナリ送信 |
| `SendMessage(string, string)` | `peerId`, `message` | `bool` | 文字列送信 |
| `Broadcast(byte[])` | `data` | `bool` | バイナリブロードキャスト |
| `Broadcast(string)` | `message` | `bool` | 文字列ブロードキャスト |

#### 状態

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `IsConnected(string)` | `peerId` | `bool` | 接続状態確認 |
| `WaitForIceConnected(string)` | `peerId` | `bool` | ICE接続待機 |

#### ピア管理

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `ClosePeer(string)` | `peerId` | `bool` | ピアクローズ |
| `CloseAllPeers()` | - | `bool` | 全ピアクローズ |

#### イベント

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `TryGetEvent(out RtcEvent?)` | `evt` | `bool` | イベントキューからポーリング |

### 例

```csharp
using var client = new RtcClient(config);

// イベントベース
client.EventReceived += (sender, evt) =>
{
    switch (evt.Type)
    {
        case "message_received":
            Console.WriteLine($"メッセージ: {Encoding.UTF8.GetString(evt.Message!)}");
            break;
        case "ice_candidate":
            SendToSignaling(evt.PeerId!, evt.Candidate!);
            break;
    }
};

// ポーリングベース
while (running)
{
    if (client.TryGetEvent(out var evt))
        HandleEvent(evt);
    Thread.Sleep(10);
}

// 操作
var offer = client.CreateOffer("peer1");
client.SetRemoteAnswer("peer1", answerSdp);
client.SendMessage("peer1", "こんにちは！");
```

---

## MediaClient

メディアサポート付き拡張クライアント（Pionバックエンドのみ）。

### スタティックプロパティ

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `IsPionBackend` | `bool` | 現在のバックエンドがPionか |

### スタティックメソッド

| メソッド | 戻り値 | 説明 |
|---------|--------|------|
| `GetSupportedCodecs()` | `MediaCodec[]` | サポートコーデック一覧 |

### インスタンスメソッド

`RtcClient`の全メソッドを継承、加えて：

| メソッド | パラメータ | 戻り値 | 説明 |
|---------|-----------|--------|------|
| `CreateMediaTrack(MediaTrackConfig)` | `config` | `string?` | トラック作成、ID返却 |
| `AddTrackToPeer(string, string)` | `peerId`, `trackId` | `bool` | ピアにトラック追加 |
| `SendMediaData(string, byte[])` | `trackId`, `rtpData` | `bool` | RTPデータ送信 |
| `RemoveMediaTrack(string)` | `trackId` | `bool` | トラック削除 |

### 例

```csharp
if (!MediaClient.IsPionBackend)
    return;

using var client = new MediaClient(config);

// コーデック取得
var codecs = MediaClient.GetSupportedCodecs();

// トラック作成
var videoTrack = client.CreateMediaTrack(new MediaTrackConfig
{
    Kind = "video",
    Codec = "vp8"
});

var audioTrack = client.CreateMediaTrack(new MediaTrackConfig
{
    Kind = "audio",
    Codec = "opus"
});

// ピアに追加
client.AddTrackToPeer("peer1", videoTrack!);
client.AddTrackToPeer("peer1", audioTrack!);

// RTP送信
client.SendMediaData(videoTrack!, rtpPacket);

// クリーンアップ
client.RemoveMediaTrack(videoTrack!);
```

---

## RtcEvent

低レベルAPI用イベントクラス。

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Type` | `string` | イベント種類文字列 |
| `PeerId` | `string?` | ピアID |
| `Sdp` | `string?` | SDP（オファー/アンサー） |
| `Data` | `string?` | Base64データ |
| `Message` | `byte[]?` | デコード済みバイト |
| `Candidate` | `IceCandidate?` | ICE候補 |
| `State` | `string?` | 状態文字列 |
| `ErrorMessage` | `string?` | エラーメッセージ |

### イベント種類

| 種類 | 説明 |
|------|------|
| `"peer_connected"` | ピア接続 |
| `"peer_disconnected"` | ピア切断 |
| `"message_received"` | データ受信 |
| `"ice_candidate"` | ICE候補準備完了 |
| `"offer_ready"` | SDPオファー準備完了 |
| `"answer_ready"` | SDPアンサー準備完了 |
| `"ice_connection_state_change"` | ICE状態変更 |
| `"data_channel_open"` | DataChannelオープン |
| `"data_channel_closed"` | DataChannelクローズ |
| `"error"` | エラー発生 |
