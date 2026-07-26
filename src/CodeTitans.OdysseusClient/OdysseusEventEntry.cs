using System.Text.Json.Serialization;

namespace CodeTitans.Odysseus;

/// <summary>
/// Model class to carry information about captured event that should be sent to Odysseus Platform.
/// </summary>
public sealed class OdysseusEventEntry
{
    [JsonPropertyName("id")]
    public Guid Id { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("platform")]
    public short? Platform { get; }

    [JsonPropertyName("session_id")]
    public Guid SessionId { get; }

    [JsonPropertyName("type")]
    public int Type { get; }

    [JsonPropertyName("stream_id")]
    public Guid? StreamId { get; }

    [JsonPropertyName("position")]
    public int Position { get; }

    [JsonPropertyName("user")]
    public string? User { get; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; }

    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, object>? Data { get; }

    [JsonPropertyName("meta")]
    public IReadOnlyDictionary<string, object>? Meta { get; }

    public OdysseusEventEntry(Guid id, string name, Guid sessionId, int type = 0, short? platform = null, Guid? streamId = null,
        int position = 0, string? user = null, DateTime? timestamp = null,
        IReadOnlyDictionary<string, object>? data = null, IReadOnlyDictionary<string, object>? meta = null)
    {
        Id = id;
        Name = name;
        SessionId = sessionId;
        Type = type;
        Platform = platform;
        StreamId = streamId;
        Position = position;
        User = user;
        Timestamp = timestamp.HasValue ? timestamp.Value.ToUniversalTime() : DateTime.UtcNow;
        Data = data;
        Meta = meta;
    }

    public override string ToString()
    {
        return string.Concat(Id.ToString("D"), ": ", Name, " [", Type, "]");
    }
}