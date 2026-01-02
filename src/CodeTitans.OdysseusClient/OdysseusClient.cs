using System.Runtime.CompilerServices;
using System.Web;

namespace CodeTitans.Odysseus;

/// <summary>
/// Odysseus Platform client capable of delivering logs and events on a timely based manner to optimize the network traffic.
/// </summary>
public sealed class OdysseusClient : IOdysseusLog, IOdysseusSession
{
    private readonly bool _stripFileName;
    private readonly string? _stripFileNamePrefix;

    private readonly OdysseusCollection<OdysseusLogEntry> _logs;
    private readonly OdysseusCollection<OdysseusEventEntry> _events;

    public OdysseusClient(string appId, string appKey, string? userId = null, Guid? sessionId = null,
        LogSeverity minSeverity = LogSeverity.Debug, short? platform = null,
        bool stripFileName = true, string? stripFileNamePrefix = null,
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

        _stripFileName = stripFileName;
        _stripFileNamePrefix = stripFileNamePrefix;
        var cf = clientFactory ?? new InternalClientFactory();

        _logs = new OdysseusCollection<OdysseusLogEntry>(endPoint: string.Concat("/api/logs/", HttpUtility.UrlEncode(appId), "/", HttpUtility.UrlEncode(appKey)),
            clientFactory: cf,
            delay: delay,
            entityName: "logs",
            internalLog: internalLog);
        _events = new OdysseusCollection<OdysseusEventEntry>(endPoint: string.Concat("/api/events/", HttpUtility.UrlEncode(appId), "/", HttpUtility.UrlEncode(appKey)),
            clientFactory: cf,
            delay: delay,
            entityName: "events",
            internalLog: internalLog);
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

    public LogSeverity MinSeverity
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
    public bool IsMatching(LogSeverity severity)
    {
        return severity >= MinSeverity;
    }

    /// <summary>
    /// Stores new log entry, if severity level is matching expectations and then uploads it to the backend.
    /// </summary>
    public OdysseusLogEntry? Add(OdysseusLogEntry entry)
    {
        if (entry.Severity < (short) MinSeverity)
        {
            return null;
        }

        if (_stripFileName)
        {
            entry.File = StripFileName(entry.File);
        }

        _logs.Add(entry);
        return entry;
    }

    /// <summary>
    /// Stores new log entry, if severity level is matching expectations and then uploads it to the backend.
    /// </summary>
    public OdysseusLogEntry? Log(string message, LogSeverity severity = LogSeverity.Debug, string? tag = null,
        [CallerFilePath] string? file = null, [CallerMemberName] string? methodName = null, [CallerLineNumber] int? line = null,
        DateTime? timestamp = null, IReadOnlyDictionary<string, object>? context = null)
    {
        if (severity < MinSeverity)
        {
            return null;
        }

        return Add(new OdysseusLogEntry(message, SessionId, severity: severity, tag: tag,
            file: StripFileName(file), methodName: methodName, line: line,
            platform: Platform, userId: UserId, timestamp: timestamp, context: context));
    }

    /// <summary>
    /// Stores a new event and then uploads it to the backend.
    /// </summary>
    public OdysseusEventEntry Add(OdysseusEventEntry entry)
    {
        _events.Add(entry);
        return entry;
    }

    /// <summary>
    /// Stores a new event and later on uploads it to the backend.
    /// </summary>
    public OdysseusEventEntry Event(string name, Guid? id = null, int type = 0, Guid? streamId = null, int position = 0,
        DateTime? timestamp = null, IReadOnlyDictionary<string, object>? data = null,
        IReadOnlyDictionary<string, object>? meta = null)
    {
        return Add(new OdysseusEventEntry(id: id ?? Guid.NewGuid(), name, sessionId: SessionId, type: type,
            platform: Platform, streamId: streamId, position: position, userId: UserId, timestamp: timestamp,
            data: data, meta: meta));
    }

    /// <summary>
    /// Wraps exception into a custom dictionary for easier setting as parameter in meta.
    /// </summary>
    public Dictionary<string, object> Wrap(Exception e)
    {
        var d = new Dictionary<string, object>();

        d.Add("type", e.GetType().Name);
        d.Add("message", e.Message);
        if (!string.IsNullOrEmpty(e.StackTrace))
        {
            d.Add("stack", e.StackTrace);
        }

        if (e.InnerException != null)
        {
            d.Add("inner", Wrap(e.InnerException));
        }

        return d;
    }

    /// <summary>
    /// Drop path from given file name.
    /// </summary>
    public string? StripFileName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (!_stripFileName)
        {
            return name;
        }

        if (string.IsNullOrEmpty(_stripFileNamePrefix))
        {
            var index = name.LastIndexOf('/');
            if (index < 0)
            {
                index = name.LastIndexOf('\\');
            }

            return name.Substring(index + 1);
        }

        var at = name.IndexOf(_stripFileNamePrefix, StringComparison.Ordinal);
        if (at < 0)
        {
            return name;
        }

        return name.Substring(at + _stripFileNamePrefix.Length);
    }
}
