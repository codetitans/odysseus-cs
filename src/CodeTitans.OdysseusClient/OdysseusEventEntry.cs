namespace CodeTitans.Odysseus;

/// <summary>
/// Model class to carry information about captured event that should be sent to Odysseus Platform.
/// </summary>
public sealed class OdysseusEventEntry
{
    public Guid Id { get; }
    public string Name { get; }
    public Guid SessionId { get; }
    public int Type { get; }
    public Guid? StreamId { get; }
    public int Position { get; }
    public string? UserId { get; }
    public DateTime? Timestamp { get; }
    public IReadOnlyDictionary<string, object>? Data { get; }
    public IReadOnlyDictionary<string, object>? Meta { get; }

    public OdysseusEventEntry(Guid id, string name, Guid sessionId, int type = 0, Guid? streamId = null,
        int position = 0, string? userId = null, DateTime? timestamp = null,
        IReadOnlyDictionary<string, object>? data = null, IReadOnlyDictionary<string, object>? meta = null)
    {
        Id = id;
        Name = name;
        SessionId = sessionId;
        Type = type;
        StreamId = streamId;
        Position = position;
        UserId = userId;
        Timestamp = timestamp;
        Data = data;
        Meta = meta;
    }

    public override string ToString()
    {
        return string.Concat(Id.ToString("D"), ": ", Name, " [", Type, "]");
    }
}