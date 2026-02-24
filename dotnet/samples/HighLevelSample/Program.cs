// ============================================
// High-Level API Sample (RtcConnection)
// SignalR風フルエントAPI
// ============================================

using Lucid.Rtc;

Console.WriteLine("=== Lucid.Rtc High-Level API Sample ===");
Console.WriteLine($"Version: {RtcConnection.Version}");

// ===== 接続作成（チェーン可） =====
using var connection = new RtcConnectionBuilder()
    .WithStunServer("stun:stun.l.google.com:19302")
    .WithStunServer("stun:stun1.l.google.com:19302")
    // .WithTurnServer("turn:example.com:3478", "username", "password")
    .WithIceConnectionTimeout(30000)
    .WithDataChannelReliable(true)
    .Build();

Console.WriteLine($"Native Available: {connection.IsConnected}");

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
        Console.WriteLine($"[ICE] Peer: {e.PeerId}, Candidate: {e.Candidate?.Candidate?[..Math.Min(50, e.Candidate.Candidate.Length)]}...");
    })
    .On<OfferReadyEvent>(e =>
    {
        Console.WriteLine($"[オファー準備完了] Peer: {e.PeerId}");
    })
    .On<AnswerReadyEvent>(e =>
    {
        Console.WriteLine($"[アンサー準備完了] Peer: {e.PeerId}");
    })
    .On<DataChannelOpenEvent>(e =>
    {
        Console.WriteLine($"[DataChannelオープン] Peer: {e.PeerId}");
    })
    .On<VideoFrameEvent>(e =>
    {
        Console.WriteLine($"[ビデオ] Peer: {e.PeerId}, Size: {e.Data?.Length ?? 0} bytes");
    })
    .On<AudioFrameEvent>(e =>
    {
        Console.WriteLine($"[オーディオ] Peer: {e.PeerId}, Size: {e.Data?.Length ?? 0} bytes");
    })
    .On<ErrorEvent>(e =>
    {
        Console.WriteLine($"[エラー] {e.Message}");
    });

// ===== ピア作成 =====
var peer = await connection.CreatePeerAsync("remote-peer");

// ===== メディア設定（チェーン可） =====
// 列挙型を使用 (推奨)
peer.WithVideo(VideoCodec.Vp8).WithAudio(AudioCodec.Opus);

// または文字列で指定
// peer.WithVideo("vp8").WithAudio("opus");

Console.WriteLine($"Peer ID: {peer.Id}");
Console.WriteLine($"State: {peer.State}");
Console.WriteLine($"Video Enabled: {peer.IsVideoEnabled}");
Console.WriteLine($"Audio Enabled: {peer.IsAudioEnabled}");

// ===== メッセージ送信 =====
peer.Send("Hello from High-Level API!");
peer.Send(new byte[] { 0x01, 0x02, 0x03, 0x04 });

// ===== ブロードキャスト =====
connection.Broadcast("Broadcast message to all peers");

// ===== 接続中のピア一覧 =====
foreach (var p in connection.GetConnectedPeers())
{
    Console.WriteLine($"Connected Peer: {p.Id}, State: {p.State}");
}

// ===== ネゴシエーション例（実際はシグナリングサーバー経由） =====
// オファー側の流れ:
// 1. オファーは自動生成される（OfferReadyEventで取得）
// 2. リモートアンサーを受信したら:
// peer.SetRemoteAnswer(remoteAnswerSdp);

// アンサー側の流れ:
// 1. リモートオファーを受信したら:
// peer.SetRemoteOffer(remoteOfferSdp);
// 2. アンサーは自動生成される（AnswerReadyEventで取得）

// ICE候補追加
// peer.AddIceCandidate(new IceCandidate { ... });

// ===== 終了 =====
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

await peer.CloseAsync();
await connection.DisposeAsync();
