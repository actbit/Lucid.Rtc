using MessagePack;

namespace Lucid.Rtc;

/// <summary>
/// Event raised when a MessagePack serialized object is received.
/// </summary>
public sealed class ObjectReceivedEvent<T> : RtcEventBase
{
    /// <summary>
    /// The deserialized object.
    /// </summary>
    public required T Value { get; init; }

    /// <summary>
    /// The raw MessagePack data.
    /// </summary>
    public byte[]? RawData { get; init; }
}

/// <summary>
/// Extension methods for MessagePack serialization.
/// </summary>
public static class RtcMessagePackExtensions
{
    private static MessagePackSerializerOptions _options = MessagePackSerializerOptions.Standard;

    /// <summary>
    /// Gets or sets the MessagePack serializer options.
    /// </summary>
    public static MessagePackSerializerOptions Options
    {
        get => _options;
        set => _options = value;
    }

    /// <summary>
    /// Send an object as MessagePack serialized data.
    /// </summary>
    public static void SendObject<T>(this Peer peer, T value)
    {
        peer.Send(MessagePackSerializer.Serialize(value, _options));
    }

    /// <summary>
    /// Broadcast an object as MessagePack serialized data.
    /// </summary>
    public static void BroadcastObject<T>(this RtcConnection connection, T value)
    {
        connection.Broadcast(MessagePackSerializer.Serialize(value, _options));
    }

    /// <summary>
    /// Register a handler for MessagePack serialized objects of type T.
    /// </summary>
    public static RtcConnection OnObject<T>(this RtcConnection connection, Action<ObjectReceivedEvent<T>> handler)
    {
        connection.On<MessageReceivedEvent>(e =>
        {
            try
            {
                var value = MessagePackSerializer.Deserialize<T>(e.Data, _options);
                if (value != null)
                {
                    var evt = new ObjectReceivedEvent<T>
                    {
                        PeerId = e.PeerId,
                        Value = value,
                        RawData = e.Data
                    };
                    evt.Peer = e.Peer;
                    handler(evt);
                }
            }
            catch (MessagePackSerializationException)
            {
                // Not a MessagePack payload, ignore
            }
        });

        return connection;
    }
}
