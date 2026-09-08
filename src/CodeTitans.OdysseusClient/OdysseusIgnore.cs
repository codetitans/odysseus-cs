namespace CodeTitans.Odysseus;

/// <summary>
/// Version of the Odysseus logger, which ignores all messages to minimize the traffic to external services.
/// </summary>
public sealed class OdysseusIgnore : IOdysseusLog, IOdysseusClient, IOdysseusSession
{
    public string? User { get; set; }
    public Guid SessionId { get; set; }
    public LogSeverity MinSeverity { get; set; }
    public short? Platform { get; set; }

    public OdysseusLogEntry? Trace(string message)
    {
        return null;
    }

    public OdysseusLogEntry? Debug(string message)
    {
        return null;
    }

    public OdysseusLogEntry? Info(string message)
    {
        return null;
    }

    public OdysseusLogEntry? Success(string message)
    {
        return null;
    }

    public OdysseusLogEntry? Warn(string message)
    {
        return null;
    }

    public OdysseusLogEntry? Warn(Exception ex, string message)
    {
        return null;
    }

    public OdysseusLogEntry? Error(string message)
    {
        return null;
    }

    public OdysseusLogEntry? Error(Exception ex, string message)
    {
        return null;
    }

    public OdysseusLogEntry? Critical(string message)
    {
        return null;
    }

    public OdysseusLogEntry? Critical(Exception ex, string message)
    {
        return null;
    }

    public OdysseusLogEntry? Log(OdysseusLogEntry entry)
    {
        return null;
    }

    public OdysseusLogEntry? Log(string message, LogSeverity severity = LogSeverity.Debug, string? file = null,
        string? methodName = null, int? line = null, int? thread = null, string? threadName = null,
        DateTime? timestamp = null, IReadOnlyDictionary<string, object>? context = null)
    {
        return null;
    }

    public OdysseusEventEntry Event(OdysseusEventEntry entry)
    {
        return entry;
    }

    public OdysseusLogEntry? Add(OdysseusLogEntry entry)
    {
        return null;
    }

    public OdysseusLogEntry? Log(string message, LogSeverity severity = LogSeverity.Debug, string? tag = null, string? file = null,
        string? methodName = null, int? line = null, int? thread = null, string? threadName = null,
        DateTime? timestamp = null, IReadOnlyDictionary<string, object>? context = null)
    {
        return null;
    }

    public OdysseusEventEntry Add(OdysseusEventEntry entry)
    {
        return entry;
    }

    OdysseusEventEntry IOdysseusClient.Event(string name, Guid? id, int type, Guid? streamId, int position,
        DateTime? timestamp, IReadOnlyDictionary<string, object>? data, IReadOnlyDictionary<string, object>? meta)
    {
        return new OdysseusEventEntry(id ?? Guid.NewGuid(), name, SessionId, type, Platform,
            streamId, position, User, timestamp, data, meta);
    }

    public Dictionary<string, object> Wrap(Exception e)
    {
        return new Dictionary<string, object>();
    }

    public Dictionary<string, object> CaptureAppInfo(IReadOnlyDictionary<string, object>? extra = null)
    {
        return new Dictionary<string, object>();
    }

    public Dictionary<string, object> CaptureDeviceInfo(IReadOnlyDictionary<string, object>? extra = null)
    {
        return new Dictionary<string, object>();
    }

    public void PersistPending()
    {
    }

    OdysseusEventEntry IOdysseusLog.Event(string name, Guid? id, int type, Guid? streamId, int position,
        DateTime? timestamp, IReadOnlyDictionary<string, object>? data, IReadOnlyDictionary<string, object>? meta)
    {
        return new OdysseusEventEntry(id ?? Guid.NewGuid(), name, SessionId, type, Platform,
            streamId, position, User, timestamp, data, meta);
    }
}
