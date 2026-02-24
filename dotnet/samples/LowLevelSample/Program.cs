// ============================================
// Low-Level API Sample (RtcClient)
// 直接FFIラッパー
// ============================================

using Lucid.Rtc;
using System.Text;

Console.WriteLine("=== Lucid.Rtc Low-Level API Sample ===");
Console.WriteLine($"Version: {RtcClient.GetVersion()}");

// ===== 設定 =====
var config = new RtcConfig
{
    StunServers = new[] {
        "stun:stun.l.google.com:19302",
        "stun:stun1.l.google.com:19302"
    },
    // TurnServerUrl = "turn:example.com:3478",
    // TurnUsername = "username",
    // TurnPassword = "password",
    IceConnectionTimeoutMs = 30000,
    DataChannelReliable = true
};

using var client = new RtcClient(config);

Console.WriteLine($"Native Available: {client.IsNativeAvailable}");

// ===== イベント（イベントハンドラ方式） =====
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
                ? Encoding.UTF8.GetString(evt.Message)
                : evt.Data ?? "(null)";
            Console.WriteLine($"[メッセージ] From {evt.PeerId}: {message}");
            break;

        case "ice_candidate":
            Console.WriteLine($"[ICE] Peer: {evt.PeerId}");
            break;

        case "offer_ready":
            Console.WriteLine($"[オファー準備完了] Peer: {evt.PeerId}");
            break;

        case "answer_ready":
            Console.WriteLine($"[アンサー準備完了] Peer: {evt.PeerId}");
            break;

        case "data_channel_open":
            Console.WriteLine($"[DataChannelオープン] Peer: {evt.PeerId}");
            break;

        case "ice_connection_state_change":
            Console.WriteLine($"[ICE状態変更] Peer: {evt.PeerId}, State: {evt.State}");
            break;

        case "error":
            Console.WriteLine($"[エラー] {evt.ErrorMessage}");
            break;
    }
};

// ===== オファー作成 =====
var offerSdp = client.CreateOffer("remote-peer");
if (offerSdp != null)
{
    Console.WriteLine($"Offer created for remote-peer");
    // シグナリングサーバー経由でofferSdpを送信
}

// ===== メッセージ送信 =====
client.SendMessage("remote-peer", "Hello from Low-Level API!");
client.SendMessage("remote-peer", new byte[] { 0x01, 0x02, 0x03, 0x04 });

// ===== ブロードキャスト =====
client.Broadcast("Broadcast message to all peers");
client.Broadcast(new byte[] { 0xFF, 0xFE, 0xFD });

// ===== 接続状態確認 =====
var isConnected = client.IsConnected("remote-peer");
Console.WriteLine($"Is Connected: {isConnected}");

// ===== ポーリング方式の例 =====
Console.WriteLine("Starting polling loop (press 'q' to quit)...");

var cts = new CancellationTokenSource();
var pollingTask = Task.Run(() =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        if (client.TryGetEvent(out var evt))
        {
            Console.WriteLine($"[Polled Event] Type: {evt?.Type}, Peer: {evt?.PeerId}");
        }
        Thread.Sleep(10);
    }
});

// ===== ネゴシエーション例（実際はシグナリングサーバー経由） =====
// アンサー側: リモートオファーを受信
// var answerSdp = client.SetRemoteOffer("remote-peer", receivedOfferSdp);

// オファー側: リモートアンサーを受信
// var success = client.SetRemoteAnswer("remote-peer", receivedAnswerSdp);

// ICE候補追加
// client.AddIceCandidate("remote-peer", new IceCandidate { ... });
// または
// client.AddIceCandidate("remote-peer", "candidate:...", "0", 0);

// ===== メディア機能（MediaClient - Pionバックエンドのみ） =====
if (MediaClient.IsPionBackend)
{
    Console.WriteLine("\n=== Media Extensions (Pion Backend) ===");

    using var mediaClient = new MediaClient(config);

    // サポートコーデック取得
    var codecs = MediaClient.GetSupportedCodecs();
    foreach (var codec in codecs)
    {
        Console.WriteLine($"Codec: {codec.MimeType} @ {codec.ClockRate}Hz, Channels: {codec.Channels}");
    }

    // メディアトラック作成
    var videoTrackId = mediaClient.CreateMediaTrack(new MediaTrackConfig
    {
        Kind = "video",
        Codec = "vp8",
        TrackId = "video-track-1",
        StreamId = "stream-1"
    });

    var audioTrackId = mediaClient.CreateMediaTrack(new MediaTrackConfig
    {
        Kind = "audio",
        Codec = "opus"
    });

    if (videoTrackId != null)
    {
        // ピアにトラック追加
        mediaClient.AddTrackToPeer("remote-peer", videoTrackId);

        // RTPデータ送信（実際のRTPパケットが必要）
        // mediaClient.SendMediaData(videoTrackId, rtpPacket);

        // トラック削除
        mediaClient.RemoveMediaTrack(videoTrackId);
    }
}
else
{
    Console.WriteLine("\nMedia extensions require Pion backend.");
}

// ===== 終了 =====
Console.WriteLine("Press 'q' to exit...");
while (Console.ReadKey(true).Key != ConsoleKey.Q) { }

cts.Cancel();
await pollingTask;

client.ClosePeer("remote-peer");
client.CloseAllPeers();
