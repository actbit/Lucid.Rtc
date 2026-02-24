// Simple P2P Chat Example
// This example demonstrates basic WebRTC usage with Lucid.Rtc
//
// Usage:
//   1. Start two instances of this program
//   2. In one instance, type 'offer' to create an offer
//   3. Copy the SDP offer to the other instance
//   4. In the second instance, type 'answer' and paste the offer
//   5. Copy the answer back to the first instance
//   6. Type 'set-answer' and paste the answer in the first instance
//   7. Exchange ICE candidates as they appear
//   8. Once connected, type messages to send them

using System.Text;
using Lucid.Rtc;

namespace SimpleChat;

class Program
{
    static RtcClient? _client;
    static string? _currentPeerId;

    static void Main(string[] args)
    {
        Console.WriteLine("=== Lucid.Rtc Simple Chat Example ===");
        Console.WriteLine("Native library available: " + (RtcClient.GetVersion() != "unknown"));
        Console.WriteLine("Library version: " + RtcClient.GetVersion());
        Console.WriteLine();

        if (!InitializeClient())
        {
            Console.WriteLine("Failed to initialize RTC client. Make sure native library is available.");
            return;
        }

        Console.WriteLine("RTC client initialized successfully!");
        Console.WriteLine();
        PrintHelp();

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(input))
                continue;

            switch (input)
            {
                case "help":
                    PrintHelp();
                    break;
                case "offer":
                    CreateOffer();
                    break;
                case "answer":
                    ProcessOffer();
                    break;
                case "set-answer":
                    SetAnswer();
                    break;
                case "ice":
                    AddIceCandidate();
                    break;
                case "status":
                    PrintStatus();
                    break;
                case "exit":
                case "quit":
                    Cleanup();
                    return;
                default:
                    if (_currentPeerId != null && input.StartsWith("msg:"))
                    {
                        var message = input[4..];
                        SendMessage(message);
                    }
                    else if (_currentPeerId != null)
                    {
                        SendMessage(input);
                    }
                    else
                    {
                        Console.WriteLine("Unknown command. Type 'help' for available commands.");
                    }
                    break;
            }
        }
    }

    static bool InitializeClient()
    {
        var config = new RtcConfig
        {
            StunServers = new[]
            {
                "stun:stun.l.google.com:19302",
                "stun:stun1.l.google.com:19302"
            },
            IceConnectionTimeoutMs = 60000
        };

        _client = new RtcClient(config);

        if (!_client.IsNativeAvailable)
        {
            _client.Dispose();
            _client = null;
            return false;
        }

        _client.EventReceived += OnEventReceived;

        // Start event processing loop
        Task.Run(ProcessEvents);

        return true;
    }

    static async Task ProcessEvents()
    {
        while (_client != null)
        {
            while (_client!.TryGetEvent(out var evt))
            {
                HandleEvent(evt);
            }
            await Task.Delay(10);
        }
    }

    static void OnEventReceived(object? sender, RtcEvent evt)
    {
        HandleEvent(evt);
    }

    static void HandleEvent(RtcEvent evt)
    {
        switch (evt.Type)
        {
            case "ice_candidate":
                Console.WriteLine();
                Console.WriteLine($"[ICE] New candidate for peer '{evt.PeerId}':");
                Console.WriteLine($"  Candidate: {evt.Candidate?.Candidate}");
                Console.WriteLine($"  Copy this ICE: ice|{evt.PeerId}|{evt.Candidate?.Candidate}|{evt.Candidate?.SdpMid}|{evt.Candidate?.SdpMlineIndex}");
                Console.Write("> ");
                break;

            case "peer_connected":
                Console.WriteLine();
                Console.WriteLine($"[CONNECTED] Peer '{evt.PeerId}' connected!");
                Console.Write("> ");
                break;

            case "peer_disconnected":
                Console.WriteLine();
                Console.WriteLine($"[DISCONNECTED] Peer '{evt.PeerId}' disconnected.");
                Console.Write("> ");
                break;

            case "data_channel_open":
                Console.WriteLine();
                Console.WriteLine($"[DATA] Channel open for peer '{evt.PeerId}'. You can now send messages!");
                Console.Write("> ");
                break;

            case "message_received":
                if (evt.Data != null)
                {
                    var bytes = Convert.FromBase64String(evt.Data);
                    var message = Encoding.UTF8.GetString(bytes);
                    Console.WriteLine();
                    Console.WriteLine($"[MESSAGE from {evt.PeerId}]: {message}");
                    Console.Write("> ");
                }
                break;

            case "ice_connection_state_change":
                Console.WriteLine();
                Console.WriteLine($"[ICE STATE] Peer '{evt.PeerId}': {evt.State}");
                Console.Write("> ");
                break;

            case "error":
                Console.WriteLine();
                Console.WriteLine($"[ERROR] {evt.Message}");
                Console.Write("> ");
                break;
        }
    }

    static void PrintHelp()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  offer       - Create a new SDP offer");
        Console.WriteLine("  answer      - Process an offer and create an answer");
        Console.WriteLine("  set-answer  - Set the remote answer (for offerer)");
        Console.WriteLine("  ice         - Add an ICE candidate (format: peer|candidate|mid|index)");
        Console.WriteLine("  status      - Show connection status");
        Console.WriteLine("  msg:<text>  - Send a message to connected peer");
        Console.WriteLine("  help        - Show this help");
        Console.WriteLine("  exit        - Exit the program");
        Console.WriteLine();
    }

    static void CreateOffer()
    {
        if (_client == null) return;

        _currentPeerId = "peer-" + Guid.NewGuid().ToString("N")[..8];
        Console.WriteLine($"Creating offer for peer: {_currentPeerId}");

        var sdp = _client.CreateOffer(_currentPeerId);
        if (sdp != null)
        {
            Console.WriteLine("Offer created! Copy this to the other peer:");
            Console.WriteLine($"---OFFER---{_currentPeerId}---");
            Console.WriteLine(sdp);
            Console.WriteLine("---ENDOFFER---");
        }
        else
        {
            Console.WriteLine("Failed to create offer.");
        }
    }

    static void ProcessOffer()
    {
        if (_client == null) return;

        Console.WriteLine("Paste the offer (including ---OFFER--- and ---ENDOFFER---):");
        var lines = new List<string>();
        string? peerId = null;

        while (true)
        {
            var line = Console.ReadLine();
            if (line == null) continue;

            if (line.StartsWith("---OFFER---"))
            {
                var parts = line.Split("---");
                if (parts.Length >= 3)
                    peerId = parts[2];
                continue;
            }

            if (line == "---ENDOFFER---")
                break;

            lines.Add(line);
        }

        if (string.IsNullOrEmpty(peerId))
        {
            Console.WriteLine("Invalid offer format. Missing peer ID.");
            return;
        }

        _currentPeerId = peerId;
        var sdp = string.Join("\r\n", lines);

        var answer = _client.SetRemoteOffer(_currentPeerId, sdp);
        if (answer != null)
        {
            Console.WriteLine("Answer created! Copy this back to the offerer:");
            Console.WriteLine($"---ANSWER---{_currentPeerId}---");
            Console.WriteLine(answer);
            Console.WriteLine("---ENDANSWER---");
        }
        else
        {
            Console.WriteLine("Failed to create answer.");
        }
    }

    static void SetAnswer()
    {
        if (_client == null || _currentPeerId == null)
        {
            Console.WriteLine("No active offer. Create an offer first.");
            return;
        }

        Console.WriteLine("Paste the answer (including ---ANSWER--- and ---ENDANSWER---):");
        var lines = new List<string>();

        while (true)
        {
            var line = Console.ReadLine();
            if (line == null) continue;

            if (line.StartsWith("---ANSWER---"))
                continue;

            if (line == "---ENDANSWER---")
                break;

            lines.Add(line);
        }

        var sdp = string.Join("\r\n", lines);

        if (_client.SetRemoteAnswer(_currentPeerId, sdp))
        {
            Console.WriteLine("Answer set successfully! Waiting for connection...");
        }
        else
        {
            Console.WriteLine("Failed to set answer.");
        }
    }

    static void AddIceCandidate()
    {
        if (_client == null) return;

        Console.WriteLine("Paste ICE candidate (format: peer|candidate|mid|index):");
        var line = Console.ReadLine();

        if (string.IsNullOrEmpty(line)) return;

        var parts = line.Split('|');
        if (parts.Length != 4)
        {
            Console.WriteLine("Invalid format. Use: peer|candidate|mid|index");
            return;
        }

        var peerId = parts[0];
        var candidate = parts[1];
        var sdpMid = parts[2];
        var sdpMlineIndex = int.Parse(parts[3]);

        if (_client.AddIceCandidate(peerId, candidate, sdpMid, sdpMlineIndex))
        {
            Console.WriteLine("ICE candidate added.");
        }
        else
        {
            Console.WriteLine("Failed to add ICE candidate.");
        }
    }

    static void SendMessage(string message)
    {
        if (_client == null || _currentPeerId == null)
        {
            Console.WriteLine("No peer connected.");
            return;
        }

        if (_client.SendMessage(_currentPeerId, message))
        {
            Console.WriteLine($"[SENT to {_currentPeerId}]: {message}");
        }
        else
        {
            Console.WriteLine("Failed to send message.");
        }
    }

    static void PrintStatus()
    {
        if (_client == null)
        {
            Console.WriteLine("Client not initialized.");
            return;
        }

        Console.WriteLine($"Native available: {_client.IsNativeAvailable}");
        Console.WriteLine($"Current peer: {_currentPeerId ?? "None"}");

        if (_currentPeerId != null)
        {
            Console.WriteLine($"Peer connected: {_client.IsConnected(_currentPeerId)}");
        }
    }

    static void Cleanup()
    {
        Console.WriteLine("Cleaning up...");
        _client?.Dispose();
        _client = null;
    }
}
