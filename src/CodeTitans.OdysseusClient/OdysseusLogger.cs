using System.Runtime.CompilerServices;

namespace CodeTitans.Odysseus;

sealed class OdysseusLogger : IOdysseusLog
{
    private readonly OdysseusClient client;
    private readonly string? tag;

    public OdysseusLogger(OdysseusClient client, string? tag)
    {
        this.client = client;
        this.tag = tag;
    }

    public string? User
    {
        get { return client.User; }
        set { client.User = value; }
    }

    public Guid SessionId
    {
        get { return client.SessionId; }
        set { client.SessionId = value; }
    }

    public OdysseusLogEntry? Trace(string message)
    {
        return client.Log(message, severity: LogSeverity.Trace);
    }

    public OdysseusLogEntry? Debug(string message)
    {
        return client.Log(message, severity: LogSeverity.Debug);
    }

    public OdysseusLogEntry? Info(string message)
    {
        return client.Log(message, severity: LogSeverity.Info);
    }

    public OdysseusLogEntry? Success(string message)
    {
        return client.Log(message, severity: LogSeverity.Success);
    }

    public OdysseusLogEntry? Warn(string message)
    {
        return client.Log(message, severity: LogSeverity.Warn);
    }

    public OdysseusLogEntry? Warn(Exception ex, string message)
    {
        return client.Log(message, severity: LogSeverity.Warn, context: CreateContectFor(ex));
    }

    public OdysseusLogEntry? Error(string message)
    {
        return client.Log(message, severity: LogSeverity.Error);
    }

    public OdysseusLogEntry? Error(Exception ex, string message)
    {
        return client.Log(message, severity: LogSeverity.Error, context: CreateContectFor(ex));
    }

    public OdysseusLogEntry? Critical(string message)
    {
        return client.Log(message, severity: LogSeverity.Critical);
    }

    public OdysseusLogEntry? Critical(Exception ex, string message)
    {
        return client.Log(message, severity: LogSeverity.Critical, context: CreateContectFor(ex));
    }

    public OdysseusLogEntry? Log(OdysseusLogEntry entry)
    {
        return client.Add(entry);
    }

    private Dictionary<string, object>? CreateContectFor(Exception? ex)
    {
        if (ex == null)
        {
            return null;
        }

        var context = new Dictionary<string, object>();
        context.Add("exception", client.Wrap(ex));
        return context;
    }

    public OdysseusLogEntry? Log(string message, LogSeverity severity = LogSeverity.Debug,
        [CallerFilePath] string? file = null, [CallerMemberName] string? methodName = null, [CallerLineNumber] int? line = null, int? thread = null, DateTime? timestamp = null,
        IReadOnlyDictionary<string, object>? context = null)
    {
        return client.Log(message, severity, tag, file, methodName, line, thread, timestamp, context);
    }

    public OdysseusEventEntry Event(OdysseusEventEntry entry)
    {
        return client.Add(entry);
    }

    public OdysseusEventEntry Event(string name, Guid? id = null, int type = 0, Guid? streamId = null, int position = 0,
        DateTime? timestamp = null, IReadOnlyDictionary<string, object>? data = null, IReadOnlyDictionary<string, object>? meta = null)
    {
        return client.Event(name, id, type, streamId, position, timestamp, data, meta);
    }
}
