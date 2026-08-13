using System.Text;

namespace CodeTitans.Odysseus;

/// <summary>
/// Owns the in-memory queue of pre-serialized JSON entries waiting to be uploaded, and - when
/// constructed with a write-ahead file - their durable backing on disk.
///
/// Disk is only touched when it's actually needed: <see cref="TakeBatch"/> hands a snapshot straight
/// out of memory, with no write beforehand. If the upload succeeds, <see cref="ConfirmSent"/> is a
/// no-op for a batch that never touched disk - on a healthy connection, entries can go from
/// <see cref="Add"/> to "successfully delivered" without a single disk write. Only
/// <see cref="Requeue"/> (the upload failed) and <see cref="PersistNow"/> (called explicitly, when
/// there's no time left for a normal upload cycle) actually write anything, and only what isn't
/// already durable.
///
/// Entries are only removed from the file once their batch has been confirmed uploaded via
/// <see cref="ConfirmSent"/>. That way, entries survive the process dying (crash, kill, no network)
/// after being persisted - the next <see cref="OdysseusStore"/> constructed against the same file
/// (i.e. on the next process launch) picks them back up automatically.
///
/// This class only manages storage - it knows nothing about the network. Callers hand a batch off
/// via <see cref="TakeBatch"/> and report the outcome back via <see cref="ConfirmSent"/> or
/// <see cref="Requeue"/>.
///
/// At most <c>maxEntries</c> entries are ever held (in memory and on disk combined) - if adding more
/// would exceed that, the oldest entries are dropped to make room, so a very long stretch without a
/// connection can't grow the pending queue (or the file backing it) without bound.
/// </summary>
sealed class OdysseusStore
{
    private readonly object _lock = new();
    private readonly List<string> _entries = new();
    private readonly string? _walFilePath;
    private readonly int _maxEntries;
    private readonly Action<string>? _internalLog;
    // entries[0, _persistedCount) are already durably written to _walFilePath; the rest is only in memory.
    private int _persistedCount;

    public OdysseusStore(string? walFilePath, int maxEntries, Action<string>? internalLog = null)
    {
        _walFilePath = walFilePath;
        _maxEntries = Math.Max(maxEntries, 1);
        _internalLog = internalLog;

        // Recover anything left over from a previous process (crash, kill, no network, ...) - this
        // is the only place recovery needs to happen, since from here on the file and the
        // in-memory queue are always kept in lock-step by TakeBatch()/ConfirmSent()/Requeue()/PersistNow().
        var recovered = ReadAllLines(walFilePath);
        if (recovered.Count > 0)
        {
            _internalLog?.Invoke($"Recovered {recovered.Count} unsent entries from a previous session");
            lock (_lock)
            {
                _entries.AddRange(recovered);
                _persistedCount = recovered.Count; // already on disk - it's where we just read them from
                EnforceLimit();
            }
        }
    }

    public void Add(string json)
    {
        lock (_lock)
        {
            _entries.Add(json);
            EnforceLimit();
        }
    }

    /// <summary>
    /// Whether there's anything - persisted or not - currently waiting to be uploaded.
    /// </summary>
    public bool HasPending
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count > 0;
            }
        }
    }

    /// <summary>
    /// Snapshots everything currently buffered - without touching disk - and clears the in-memory
    /// queue, handing ownership of that batch to the caller until it reports back via
    /// <see cref="ConfirmSent"/> or <see cref="Requeue"/>. Entries added while the batch is still
    /// outstanding accumulate separately and are unaffected.
    /// </summary>
    public TakenBatch TakeBatch()
    {
        lock (_lock)
        {
            var batch = new List<string>(_entries);
            var batchPersistedCount = _persistedCount;
            _entries.Clear();
            _persistedCount = 0;
            return new TakenBatch(batch, batchPersistedCount);
        }
    }

    /// <summary>
    /// Reports that a batch obtained from <see cref="TakeBatch"/> was successfully uploaded. If it
    /// never touched disk (the common case on a healthy connection), this is a no-op; otherwise it
    /// drops the now-confirmed prefix from the durable store.
    /// </summary>
    public void ConfirmSent(TakenBatch batch)
    {
        if (batch.PersistedCount <= 0)
        {
            return;
        }

        lock (_lock)
        {
            RemoveFirstLines(_walFilePath, batch.PersistedCount);
        }
    }

    /// <summary>
    /// Reports that a batch obtained from <see cref="TakeBatch"/> failed to upload: persists
    /// whatever part of it isn't already durable, then puts the whole batch back at the front of
    /// the queue for a later retry.
    /// </summary>
    public void Requeue(TakenBatch batch)
    {
        lock (_lock)
        {
            if (batch.PersistedCount < batch.Items.Count)
            {
                AppendLines(_walFilePath, batch.Items.Skip(batch.PersistedCount));
            }

            _entries.InsertRange(0, batch.Items);
            _persistedCount = batch.Items.Count;
            EnforceLimit();
        }
    }

    // must be called while already holding `_lock`
    private void EnforceLimit()
    {
        var overflow = _entries.Count - _maxEntries;
        if (overflow <= 0)
        {
            return;
        }

        // Drop the oldest `overflow` entries to make room. If any of them were already durably
        // persisted, trim the same count from the front of the file too, so it stays in lock-step
        // with memory instead of accumulating entries we've decided to discard.
        var droppedPersisted = Math.Min(overflow, _persistedCount);
        if (droppedPersisted > 0)
        {
            RemoveFirstLines(_walFilePath, droppedPersisted);
        }

        _entries.RemoveRange(0, overflow);
        _persistedCount = Math.Max(0, _persistedCount - overflow);

        _internalLog?.Invoke($"Pending queue exceeded {_maxEntries} entries - dropped the oldest {overflow}");
    }

    /// <summary>
    /// Forces everything currently buffered to disk right now, regardless of upload state. Meant to
    /// be called explicitly, when there's no time left to wait for a normal upload cycle.
    /// </summary>
    public void PersistNow()
    {
        lock (_lock)
        {
            if (_persistedCount < _entries.Count)
            {
                AppendLines(_walFilePath, _entries.Skip(_persistedCount));
                _persistedCount = _entries.Count;
            }
        }
    }

    /// <summary>
    /// A batch of entries taken via <see cref="TakeBatch"/>, along with how many of its leading
    /// entries were already durable on disk at the time it was taken.
    /// </summary>
    public sealed class TakenBatch
    {
        public IReadOnlyList<string> Items { get; }
        public int PersistedCount { get; }

        public TakenBatch(IReadOnlyList<string> items, int persistedCount)
        {
            Items = items;
            PersistedCount = persistedCount;
        }

        public bool IsEmpty => Items.Count == 0;
    }

    // --- Write-ahead file persistence, so unsent entries survive the process dying -----------

    private void AppendLines(string? path, IEnumerable<string> lines)
    {
        var materialized = lines as ICollection<string> ?? lines.ToList();
        if (path == null || materialized.Count == 0)
        {
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var writer = new StreamWriter(path, append: true, Encoding.UTF8);
            foreach (var line in materialized)
            {
                writer.WriteLine(line);
            }
        }
        catch (Exception ex)
        {
            _internalLog?.Invoke($"Failed to persist pending entries: {ex.Message}");
        }
    }

    private void RemoveFirstLines(string? path, int count)
    {
        if (path == null || count <= 0 || !File.Exists(path))
        {
            return;
        }

        try
        {
            var remaining = File.ReadAllLines(path, Encoding.UTF8).Skip(count).ToArray();
            if (remaining.Length == 0)
            {
                File.Delete(path);
                return;
            }

            File.WriteAllLines(path, remaining, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _internalLog?.Invoke($"Failed to trim pending store: {ex.Message}");
        }
    }

    private List<string> ReadAllLines(string? path)
    {
        var lines = new List<string>();
        if (path == null || !File.Exists(path))
        {
            return lines;
        }

        try
        {
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }
        }
        catch (Exception ex)
        {
            _internalLog?.Invoke($"Failed to read pending store: {ex.Message}");
        }

        return lines;
    }
}
