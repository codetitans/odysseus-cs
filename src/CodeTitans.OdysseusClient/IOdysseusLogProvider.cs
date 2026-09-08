namespace CodeTitans.Odysseus;

/// <summary>
/// Simple interface to provide new loggers for given tags.
/// </summary>
public interface IOdysseusLogProvider
{
    /// <summary>
    /// Creates a new instance of the logger for a given tag.
    /// </summary>
    IOdysseusLog Create(string? tag = null);
}
