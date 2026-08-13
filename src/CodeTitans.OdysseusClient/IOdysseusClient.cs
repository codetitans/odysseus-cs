using System.Runtime.CompilerServices;

namespace CodeTitans.Odysseus;

public interface IOdysseusClient
{
    /// <summary>
    /// Adds a new log entry. Returns that log entry, if stored or null, when log level was too high to keep it stored.
    /// </summary>
    OdysseusLogEntry? Add(OdysseusLogEntry entry);

    /// <summary>
    /// Adds a new log entry. Returns that log entry, if stored or null, when a log level was too high to keep it stored.
    /// </summary>
    OdysseusLogEntry? Log(string message, LogSeverity severity = LogSeverity.Debug, string? tag = null,
        [CallerFilePath] string? file = null, [CallerMemberName] string? methodName = null, [CallerLineNumber] int? line = null,
        int? thread = null, string? threadName = null, DateTime? timestamp = null, IReadOnlyDictionary<string, object>? context = null);

    /// <summary>
    /// Adds a new event.
    /// </summary>
    OdysseusEventEntry Add(OdysseusEventEntry entry);

    /// <summary>
    /// Adds a new event.
    /// </summary>
    OdysseusEventEntry Event(string name, Guid? id = null, int type = 0, Guid? streamId = null, int position = 0,
        DateTime? timestamp = null, IReadOnlyDictionary<string, object>? data = null,
        IReadOnlyDictionary<string, object>? meta = null);

    /// <summary>
    /// Wraps exception into a custom dictionary for easier setting as a parameter in meta.
    /// </summary>
    Dictionary<string, object> Wrap(Exception e);

    /// <summary>
    /// Captures details about the currently running app (version, debug/release build, ...) - see
    /// <see cref="OdysseusDeviceInfo.CaptureAppInfo"/>. Any entries also present in <paramref name="extra"/>
    /// are overridden by it.
    /// </summary>
    Dictionary<string, object> CaptureAppInfo(IReadOnlyDictionary<string, object>? extra = null);

    /// <summary>
    /// Captures details about the current device/host (OS, runtime, memory, locale, ...) - see
    /// <see cref="OdysseusDeviceInfo.CaptureDeviceInfo"/>. Any entries also present in <paramref name="extra"/>
    /// are overridden by it.
    /// </summary>
    Dictionary<string, object> CaptureDeviceInfo(IReadOnlyDictionary<string, object>? extra = null);

    /// <summary>
    /// Forces every not-yet-uploaded log entry and event currently held only in memory to durable
    /// storage right now (a local disk write - no network involved). A no-op if this client was
    /// constructed without a <c>storageDirectory</c> (nothing durable to write to).
    /// </summary>
    void PersistPending();
}
