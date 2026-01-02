using System.Runtime.CompilerServices;

namespace CodeTitans.Odysseus;

public interface IOdysseusLog
{
    /// <summary>
    /// Adds new log entry. Returns that log entry, if stored or null, when log level was too high to keep it stored.
    /// </summary>
    OdysseusLogEntry? Add(OdysseusLogEntry entry);

    /// <summary>
    /// Adds new log entry. Returns that log entry, if stored or null, when log level was too high to keep it stored.
    /// </summary>
    OdysseusLogEntry? Log(string message, LogSeverity severity = LogSeverity.Debug, string? tag = null,
        [CallerFilePath] string? file = null, [CallerMemberName] string? methodName = null, [CallerLineNumber] int? line = null,
        DateTime? timestamp = null, IReadOnlyDictionary<string, object>? context = null);

    /// <summary>
    /// Adds new event.
    /// </summary>
    OdysseusEventEntry Add(OdysseusEventEntry entry);

    /// <summary>
    /// Adds new event.
    /// </summary>
    OdysseusEventEntry Event(string name, Guid? id = null, int type = 0, Guid? streamId = null, int position = 0,
        DateTime? timestamp = null, IReadOnlyDictionary<string, object>? data = null,
        IReadOnlyDictionary<string, object>? meta = null);

    /// <summary>
    /// Wraps exception into a custom dictionary for easier setting as parameter in meta.
    /// </summary>
    Dictionary<string, object> Wrap(Exception e);
}
