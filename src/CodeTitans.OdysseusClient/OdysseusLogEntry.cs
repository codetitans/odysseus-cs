namespace CodeTitans.Odysseus;

/// <summary>
/// Model class to carry information about log entry generated during application run that should be sent to Odysseus Platform.
/// </summary>
public sealed class OdysseusLogEntry
{
    public string Message { get; }
    public Guid SessionId { get; }
    public int Severity { get; }
    public string? Tag { get; }
    public string? File { get; }
    public int? Line { get; }
    public string? UserId { get; }
    public DateTime Timestamp { get; }
    public IReadOnlyDictionary<string, object>? Context { get; }

    public OdysseusLogEntry(string message, Guid sessionId, int severity = 0, string? tag = null, string? file = null,
        int? line = null, string? userId = null, DateTime? timestamp = null, IReadOnlyDictionary<string, object>? context = null)
    {
        Message = message;
        SessionId = sessionId;
        Severity = severity;
        Tag = tag;
        File = file;
        Line = line;
        UserId = userId;
        Timestamp = timestamp.HasValue ? timestamp.Value.ToUniversalTime() : DateTime.UtcNow;
        Context = context;
    }

    public override string ToString()
    {
        return string.Concat(Severity, ": ", Message);
    }
}
