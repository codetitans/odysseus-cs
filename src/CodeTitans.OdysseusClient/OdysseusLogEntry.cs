using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CodeTitans.Odysseus;

/// <summary>
/// Model class to carry information about log entry generated during application run that should be sent to Odysseus Platform.
/// </summary>
public sealed class OdysseusLogEntry
{
    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("session_id")]
    public Guid SessionId { get; }

    [JsonPropertyName("severity")]
    public short Severity { get; }

    [JsonPropertyName("tag")]
    public string? Tag { get; }

    [JsonPropertyName("platform")]
    public short? Platform { get; }

    [JsonPropertyName("file")]
    public string? File { get; }

    [JsonPropertyName("method")]
    public string? MethodName { get; }

    [JsonPropertyName("line")]
    public int? Line { get; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; }

    [JsonPropertyName("context")]
    public IReadOnlyDictionary<string, object>? Context { get; }

    public OdysseusLogEntry(string message, Guid sessionId, LogSeverity severity = LogSeverity.Trace, string? tag = null, short? platform = null,
        [CallerFilePath] string? file = null, [CallerMemberName] string? methodName = null, [CallerLineNumber] int? line = null,
        string? userId = null, DateTime? timestamp = null, IReadOnlyDictionary<string, object>? context = null)
    {
        Message = message;
        SessionId = sessionId;
        Severity = (short) severity;
        Platform = platform;
        Tag = tag;
        File = file;
        MethodName = methodName;
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
