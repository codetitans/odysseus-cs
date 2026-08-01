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
    public string? File { get; set; }

    [JsonPropertyName("method")]
    public string? MethodName { get; }

    [JsonPropertyName("line")]
    public int? Line { get; }

    [JsonPropertyName("thread")]
    public int? Thread { get; }

    [JsonPropertyName("thread_name")]
    public string? ThreadName { get; }

    [JsonPropertyName("user")]
    public string? User { get; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; }

    [JsonPropertyName("context")]
    public IReadOnlyDictionary<string, object>? Context { get; }

    public OdysseusLogEntry(string message, Guid sessionId, LogSeverity severity = LogSeverity.Trace, string? tag = null, short? platform = null,
        [CallerFilePath] string? file = null, [CallerMemberName] string? methodName = null, [CallerLineNumber] int? line = null,
        int? thread = null, string? threadName = null, string? user = null, DateTime? timestamp = null, IReadOnlyDictionary<string, object>? context = null)
    {
        Message = message;
        SessionId = sessionId;
        Severity = (short) severity;
        Platform = platform;
        Tag = tag;
        File = file;
        MethodName = methodName;
        Line = line;
        Thread = thread ?? System.Threading.Thread.CurrentThread.ManagedThreadId;
        ThreadName = !string.IsNullOrWhiteSpace(threadName) ? threadName : System.Threading.Thread.CurrentThread.Name;
        User = user;
        Timestamp = timestamp.HasValue ? timestamp.Value.ToUniversalTime() : DateTime.UtcNow;
        Context = context;
    }

    public override string ToString()
    {
        return string.Concat(Severity, ": ", Message);
    }
}
