// ============================================
// 古いAPI (RtcClient) - Shallow Wrapper
// ============================================

using Lucid.Rtc;

// ===== 作成 =====
var config = new RtcConfig
{
    StunServers = new[] { "stun:stun.l.google.com:19302" },
    TurnServerUrl = "turn:example.com:3478",
    TurnUsername = "username",
    TurnPassword = "password",
    IceConnectionTimeoutMs = 30000
};

var client = new RtcClient(config);

// ===== イベント（ポーリング方式） =====
client.EventReceived += (sender, evt) =>
{
    switch (evt.Type)
    {
        case "peer_connected":
            Console.WriteLine($"[接続] Peer: {evt.PeerId}");
            break;

        case "peer_disconnected":
            Console.WriteLine($"[切断] Peer: {evt.PeerId}");
            break;

        case "message_received":
            var message = evt.Message != null
                ? System.Text.Encoding.UTF8.GetString(evt.Message)
                : evt.Data;
            Console.WriteLine($"[メッセージ] From {evt.PeerId}: {message}");
            break;

        case "ice_candidate":
            SendToSignalingServer(evt.PeerId!, evt.Candidate!);
            break;

        case "error":
            Console.WriteLine($"[エラー] {evt.ErrorMessage}");
            break;
    }
};

// または手動でポーリング
while (running)
{
    if (client.TryGetEvent(out var evt))
    {
        HandleEvent(evt);
    }
    Thread.Sleep(10);
}

// ===== オファー作成 =====
var offerSdp = client.CreateOffer("remote-peer");
if (offerSdp != null)
{
    // シグナリングサーバー経由でofferSdpを送信
    SendOfferToSignaling("remote-peer", offerSdp);
}

// ===== リモートオファー処理（アンサー側） =====
var answerSdp = client.SetRemoteOffer("remote-peer", receivedOfferSdp);
if (answerSdp != null)
{
    // シグナリングサーバー経由でanswerSdpを送信
    SendAnswerToSignaling("remote-peer", answerSdp);
}

// ===== リモートアンサー処理（オファー側） =====
var success = client.SetRemoteAnswer("remote-peer", receivedAnswerSdp);

// ===== ICE候補追加 =====
client.AddIceCandidate("remote-peer", iceCandidate);
// または個別パラメータで
client.AddIceCandidate("remote-peer", "candidate:...", "0", 0);

// ===== メッセージ送信 =====
client.SendMessage("remote-peer", "こんにちは！");
client.SendMessage("remote-peer", binaryData);

// ===== ブロードキャスト =====
client.Broadcast("全体メッセージ");
client.Broadcast(binaryData);

// ===== 接続状態確認 =====
var isConnected = client.IsConnected("remote-peer");
var iceConnected = client.WaitForIceConnected("remote-peer");

// ===== ピアクローズ =====
client.ClosePeer("remote-peer");
client.CloseAllPeers();

// ===== 終了 =====
client.Dispose();


// ============================================
// メディア機能（MediaClient - Pionバックエンドのみ）
// ============================================

if (MediaClient.IsPionBackend)
{
    var mediaClient = new MediaClient(config);

    // サポートコーデック取得
    var codecs = MediaClient.GetSupportedCodecs();
    foreach (var codec in codecs)
    {
        Console.WriteLine($"{codec.MimeType} - ClockRate: {codec.ClockRate}");
    }

    // メディアトラック作成
    var trackId = mediaClient.CreateMediaTrack(new MediaTrackConfig
    {
        Kind = "video",
        Codec = "vp8",
        TrackId = "video-track-1"
    });

    // ピアにトラック追加
    mediaClient.AddTrackToPeer("remote-peer", trackId!);

    // RTPデータ送信
    mediaClient.SendMediaData(trackId!, rtpPacket);

    // トラック削除
    mediaClient.RemoveMediaTrack(trackId!);

    mediaClient.Dispose();
}


// ============================================
// バージョン取得
// ============================================

var version = RtcClient.GetVersion();
Console.WriteLine($"Lucid.Rtc Version: {version}");
