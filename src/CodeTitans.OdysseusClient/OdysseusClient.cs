using System.Web;

namespace CodeTitans.Odysseus;

/// <summary>
/// Odyssus Platform client capable of delivering logs and events on a timely based manner to optimize the network traffic.
/// </summary>
public sealed class OdysseusClient
{
    private readonly Action<string>? _internalLog;

    private readonly OdysseusCollection<OdysseusLogEntry> _logs;
    private readonly OdysseusCollection<OdysseusEventEntry> _events;

    public OdysseusClient(string appId, string appKey, string? userId = null, Guid? sessionId = null,
        int minSeverity = 1, short? platform = null,
        IHttpClientFactory? clientFactory = null, int delay = 5, Action<string>? internalLog = null)
    {
        if (string.IsNullOrWhiteSpace(appId))
            throw new ArgumentNullException(nameof(appId));
        if (string.IsNullOrWhiteSpace(appKey))
            throw new ArgumentNullException(nameof(appKey));

        UserId = userId;
        SessionId = sessionId ?? Guid.NewGuid();
        MinSeverity = minSeverity;
        Platform = platform;

        _internalLog = internalLog;
        var cf = clientFactory ?? new InternalClientFactory();

        _logs = new OdysseusCollection<OdysseusLogEntry>(endPoint: string.Concat("/api/logs/", HttpUtility.UrlEncode(appId), "/", HttpUtility.UrlEncode(appKey)),
            clientFactory: cf,
            delay: delay,
            entityName: "logs",
            internalLog: _internalLog);
        _events = new OdysseusCollection<OdysseusEventEntry>(endPoint: string.Concat("/api/events/", HttpUtility.UrlEncode(appId), "/", HttpUtility.UrlEncode(appKey)),
            clientFactory: cf,
            delay: delay,
            entityName: "events",
            internalLog: _internalLog);
    }

    public string? UserId
    {
        get;
        set;
    }

    public Guid SessionId
    {
        get;
        set;
    }

    public int MinSeverity
    {
        get;
        set;
    }

    public short? Platform
    {
        get;
        set;
    }

    /// <summary>
    /// Internal simple way of providing the default http-client.
    /// </summary>
    private sealed class InternalClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }

    /// <summary>
    /// Checks, if given severity level is good for storing.
    /// </summary>
    public bool IsMatching(int severity)
    {
        return severity >= MinSeverity;
    }

    /// <summary>
    /// Stores new log entry, if severity level is matching expectations and then uploads it to the backend.
    /// </summary>
    public OdysseusLogEntry? Add(OdysseusLogEntry entry)
    {
        if (entry.Severity < MinSeverity)
        {
            return null;
        }

        _logs.Add(entry);
        return entry;
    }

    /// <summary>
    /// Stores new log entry, if severity level is matching expectations and then uploads it to the backend.
    /// </summary>
    public OdysseusLogEntry? Log(string message, int severity = 1, string? tag = null, string? file = null, int? line = null,
        DateTime? timestamp = null, IReadOnlyDictionary<string, object>? context = null)
    {
        if (severity < MinSeverity)
        {
            return null;
        }

        return Add(new OdysseusLogEntry(message, SessionId, severity: severity, tag: tag, file: file, line: line,
            platform: Platform, userId: UserId, timestamp: timestamp, context: context));
    }

    /// <summary>
    /// Stores a new event and then uploads it to the backend.
    /// </summary>
    public OdysseusEventEntry? Add(OdysseusEventEntry entry)
    {
        _events.Add(entry);
        return entry;
    }

    /// <summary>
    /// Stores a new event and later on uploads it to the backend.
    /// </summary>
    public OdysseusEventEntry? Event(string name, Guid? id = null, int type = 0, Guid? streamId = null, int position = 0,
        DateTime? timestamp = null, IReadOnlyDictionary<string, object>? data = null,
        IReadOnlyDictionary<string, object>? meta = null)
    {
        return Add(new OdysseusEventEntry(id: id ?? Guid.NewGuid(), name, sessionId: SessionId, type: type,
            platform: Platform, streamId: streamId, position: position, userId: UserId, timestamp: timestamp,
            data: data, meta: meta));
    }
}
