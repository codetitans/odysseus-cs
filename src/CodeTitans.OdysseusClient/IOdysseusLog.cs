using System.Runtime.CompilerServices;

namespace CodeTitans.Odysseus;

/// <summary>
/// Classic Odysseus Logger.
/// </summary>
public interface IOdysseusLog
{
    /// <summary>
    /// Gets or sets the current user.
    /// </summary>
    string? User { get; set; }

    /// <summary>
    /// Gets or sets the current session.
    /// </summary>
    Guid SessionId { get; set; }

    OdysseusLogEntry? Trace(string message);
    OdysseusLogEntry? Debug(string message);
    OdysseusLogEntry? Info(string message);
    OdysseusLogEntry? Success(string message);
    OdysseusLogEntry? Warn(string message);
    OdysseusLogEntry? Warn(Exception ex, string message);
    OdysseusLogEntry? Error(string message);
    OdysseusLogEntry? Error(Exception ex, string message);
    OdysseusLogEntry? Critical(string message);
    OdysseusLogEntry? Critical(Exception ex, string message);

    OdysseusLogEntry? Log(OdysseusLogEntry entry);
    OdysseusLogEntry? Log(string message, LogSeverity severity = LogSeverity.Debug,
        [CallerFilePath] string? file = null, [CallerMemberName] string? methodName = null, [CallerLineNumber] int? line = null,
        int? thread = null, DateTime? timestamp = null, IReadOnlyDictionary<string, object>? context = null);

    OdysseusEventEntry Event(OdysseusEventEntry entry);
    OdysseusEventEntry Event(string name, Guid? id = null, int type = 0, Guid? streamId = null, int position = 0,
        DateTime? timestamp = null, IReadOnlyDictionary<string, object>? data = null,
        IReadOnlyDictionary<string, object>? meta = null);
}
