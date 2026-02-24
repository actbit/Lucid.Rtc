// ============================================
// 新しいAPI (RtcConnection) - SignalR風フルエントAPI
// ============================================

using Lucid.Rtc;

// ===== 作成（チェーン可） =====
var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .WithStunServer("stun:stun1.l.google.com:19302")
    .WithTurnServer("turn:example.com:3478", "username", "password")
    .Build();

// ===== イベント登録（チェーン可） =====
connection
    .On<PeerConnectedEvent>(e =>
    {
        Console.WriteLine($"[接続] Peer: {e.PeerId}");
    })
    .On<PeerDisconnectedEvent>(e =>
    {
        Console.WriteLine($"[切断] Peer: {e.PeerId}");
    })
    .On<MessageReceivedEvent>(e =>
    {
        Console.WriteLine($"[メッセージ] From {e.PeerId}: {e.DataAsString}");
    })
    .On<IceCandidateEvent>(e =>
    {
        // シグナリングサーバーにICE候補を送信
        SendToSignalingServer(e.PeerId, e.Candidate);
    })
    .On<VideoFrameEvent>(e =>
    {
        // ビデオフレーム受信
        DisplayVideoFrame(e.Data);
    })
    .On<AudioFrameEvent>(e =>
    {
        // オーディオフレーム受信
        PlayAudio(e.Data);
    })
    .On<ErrorEvent>(e =>
    {
        Console.WriteLine($"[エラー] {e.Message}");
    });

// ===== ピア作成（メディア付き、チェーン可） =====
var peer = await connection.CreatePeerAsync("remote-peer")
    .WithVideo("vp8")      // "vp8", "vp9", "h264", "av1"
    .WithAudio("opus");    // "opus", "pcmu", "pcma"

// ===== ネゴシエーション（チェーン可） =====
// オファー側
peer.SetRemoteAnswer(remoteAnswer);

// または アンサー側
peer.SetRemoteOffer(remoteOffer);

// ICE候補追加
peer.AddIceCandidate(iceCandidate);

// ===== データ送信（チェーンなし） =====
peer.Send("こんにちは！");
peer.Send(binaryData);

// メディア送信
peer.SendVideo(rtpPacket);
peer.SendAudio(rtpPacket);

// 全員にブロードキャスト
connection.Broadcast("全体メッセージ");

// ===== ピア情報 =====
Console.WriteLine($"ID: {peer.Id}");
Console.WriteLine($"接続状態: {peer.IsConnected}");
Console.WriteLine($"状態: {peer.State}");
Console.WriteLine($"ビデオ有効: {peer.IsVideoEnabled}");
Console.WriteLine($"オーディオ有効: {peer.IsAudioEnabled}");

// ===== 接続中のピア一覧 =====
foreach (var p in connection.GetConnectedPeers())
{
    Console.WriteLine($"Connected: {p.Id}");
}

// ===== 終了 =====
await peer.CloseAsync();
await connection.DisposeAsync();


// ============================================
// ヘルパーメソッド（シグナリング用）
// ============================================

void SendToSignalingServer(string peerId, IceCandidate candidate)
{
    // WebSocketやHTTPでシグナリングサーバーに送信
}

void DisplayVideoFrame(byte[] rtpData)
{
    // RTPパケットをデコードして表示
}

void PlayAudio(byte[] rtpData)
{
    // RTPパケットをデコードして再生
}
