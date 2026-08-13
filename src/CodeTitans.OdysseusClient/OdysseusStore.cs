using System.Text;

namespace CodeTitans.Odysseus;

/// <summary>
/// Owns the queue of pre-serialized JSON entries waiting to be uploaded, and - when constructed
/// with a write-ahead directory - their durable backing on disk, split across multiple small chunk
/// files instead of one ever-growing file.
///
/// At any time there's at most one "active" chunk, held only in memory, that new entries are
/// appended to. Once it reaches <c>entriesPerFile</c> entries it's written to disk in one go,
/// registered as a "closed" chunk (only its file identity is kept in memory from then on - never
/// its content), and a fresh empty active chunk takes over. This means:
/// <list type="bullet">
/// <item>only ever up to <c>entriesPerFile</c> entries sit in memory at once, no matter how big the
/// backlog on disk gets;</item>
/// <item>closed chunks are written exactly once and never rewritten - a long outage with entries
/// still streaming in keeps producing new, small, one-shot writes instead of repeatedly rewriting a
/// single growing file;</item>
/// <item><see cref="TakeBatch"/> still hands the active chunk straight out of memory with no write
/// beforehand (see <see cref="ConfirmSent"/>) - on a healthy connection, nothing needs to touch disk
/// at all, exactly as before chunking was introduced.</item>
/// </list>
/// At most <c>maxFiles</c> closed chunks are ever kept - if closing a new one would exceed that, the
/// oldest chunk (and its up-to-<c>entriesPerFile</c> entries) is dropped first. Total capacity is
/// therefore <c>entriesPerFile * maxFiles</c> entries.
///
/// This class only manages storage - it knows nothing about the network. Callers hand a batch off
/// via <see cref="TakeBatch"/> (always the oldest available data first) and report the outcome back
/// via <see cref="ConfirmSent"/> or <see cref="Requeue"/>.
/// </summary>
sealed class OdysseusStore
{
    private const string ChunkFileSuffix = ".jsonl";

    private readonly object _lock = new();
    private readonly string? _walDir;
    private readonly int _entriesPerFile;
    private readonly int _maxFiles;
    private readonly Action<string>? _internalLog;

    // The active chunk: entirely in memory until it's closed (rotated out because it reached
    // _entriesPerFile) or an upload attempt needs it durable ahead of that.
    private readonly List<string> _activeChunk = new();
    private int _activeChunkPersistedCount;
    private long _activeChunkSequence;

    // Closed chunks: fully on disk, not kept in memory - only their file identity, oldest first.
    private readonly LinkedList<long> _closedChunks = new();

    public OdysseusStore(string? walDir, int entriesPerFile, int maxFiles, Action<string>? internalLog = null)
    {
        _walDir = walDir;
        _entriesPerFile = Math.Max(entriesPerFile, 1);
        _maxFiles = Math.Max(maxFiles, 1);
        _internalLog = internalLog;

        // Recover chunk files left over from a previous process (crash, kill, no network, ...).
        // Every file found is treated as a closed chunk, whether or not it was ever filled to
        // _entriesPerFile - simpler than trying to resume filling a partial one, and still fully
        // durable. A fresh, empty active chunk starts right after.
        var recovered = ListChunkSequences(walDir);
        long nextSequence = 0;
        if (recovered.Count > 0)
        {
            _internalLog?.Invoke($"Recovered {recovered.Count} pending chunk file(s) from a previous session");
            lock (_lock)
            {
                foreach (var sequence in recovered)
                {
                    _closedChunks.AddLast(sequence);
                }
                EvictExcessChunks(); // in case maxFiles was lowered since the last run
            }
            nextSequence = recovered[^1] + 1;
        }
        _activeChunkSequence = nextSequence;
    }

    public void Add(string json)
    {
        lock (_lock)
        {
            _activeChunk.Add(json);
            RotateIfFull();
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
                return _activeChunk.Count > 0 || _closedChunks.Count > 0;
            }
        }
    }

    /// <summary>
    /// Hands out the oldest available batch - a closed chunk if one exists, otherwise whatever's in
    /// the active chunk - without touching disk on its own. Ownership passes to the caller until it
    /// reports back via <see cref="ConfirmSent"/> or <see cref="Requeue"/>. Taking the active chunk
    /// always starts a fresh one behind it (with a new identity), so entries added afterward never
    /// get mixed up with this batch.
    /// </summary>
    public TakenBatch TakeBatch()
    {
        lock (_lock)
        {
            if (_closedChunks.Count > 0)
            {
                var oldestClosed = _closedChunks.First!.Value;
                return TakenBatch.OfClosedChunk(ReadAllLines(ChunkFile(oldestClosed)), oldestClosed);
            }

            if (_activeChunk.Count == 0)
            {
                return TakenBatch.Empty;
            }

            var items = new List<string>(_activeChunk);
            var persisted = _activeChunkPersistedCount;
            var sequence = _activeChunkSequence;

            _activeChunk.Clear();
            _activeChunkPersistedCount = 0;
            _activeChunkSequence = sequence + 1;

            return TakenBatch.OfActiveChunk(items, sequence, persisted);
        }
    }

    /// <summary>
    /// Reports that a batch obtained from <see cref="TakeBatch"/> was successfully uploaded. If it
    /// never touched disk (the common case on a healthy connection), this is a no-op; otherwise its
    /// file is deleted.
    /// </summary>
    public void ConfirmSent(TakenBatch batch)
    {
        lock (_lock)
        {
            if (batch.FromClosedChunk)
            {
                _closedChunks.Remove(batch.Sequence);
                DeleteChunkFile(batch.Sequence);
            }
            else if (batch.PersistedCount > 0)
            {
                DeleteChunkFile(batch.Sequence);
            }
        }
    }

    /// <summary>
    /// Reports that a batch obtained from <see cref="TakeBatch"/> failed to upload, so it needs a
    /// later retry. A closed-chunk batch needs no change at all - its file, if still present, is
    /// already exactly where it needs to be, still the oldest thing pending. An active-chunk batch's
    /// sequence was already permanently retired when it was taken, so it can't merge back into
    /// whatever the (now different) active chunk has become in the meantime; instead it's persisted
    /// (whatever part of it wasn't already durable) and registered as its own closed chunk, at the
    /// front since it's the oldest data around.
    /// </summary>
    public void Requeue(TakenBatch batch)
    {
        lock (_lock)
        {
            if (batch.FromClosedChunk)
            {
                return;
            }

            if (_walDir == null)
            {
                // Nothing durable to fall back to - put the actual content straight back into
                // memory instead of registering a chunk with no file behind it.
                _activeChunk.InsertRange(0, batch.Items);
                RotateIfFull();
                return;
            }

            if (batch.PersistedCount < batch.Items.Count)
            {
                AppendLines(ChunkFile(batch.Sequence), batch.Items.Skip(batch.PersistedCount));
            }

            _closedChunks.AddFirst(batch.Sequence);
            EvictExcessChunks();
        }
    }

    /// <summary>
    /// Forces the active chunk to disk right now, regardless of upload state - without rotating it
    /// out. Meant to be called explicitly, when there's no time left to wait for a normal upload
    /// cycle.
    /// </summary>
    public void PersistNow()
    {
        lock (_lock)
        {
            PersistActiveChunkLocked();
        }
    }

    // must be called while already holding `_lock`
    private void RotateIfFull()
    {
        if (_walDir == null)
        {
            // No disk to hold "closed" chunks on - just cap the total in-memory size directly,
            // dropping the oldest as needed, the same way a single-buffer store would.
            var overflow = _activeChunk.Count - _entriesPerFile * _maxFiles;
            if (overflow > 0)
            {
                _activeChunk.RemoveRange(0, overflow);
            }
            return;
        }

        if (_activeChunk.Count >= _entriesPerFile)
        {
            CloseActiveChunk();
        }
    }

    // must be called while already holding `_lock`
    private void CloseActiveChunk()
    {
        PersistActiveChunkLocked();
        _closedChunks.AddLast(_activeChunkSequence);
        _activeChunk.Clear();
        _activeChunkPersistedCount = 0;
        _activeChunkSequence++;
        EvictExcessChunks();
    }

    // must be called while already holding `_lock`
    private void PersistActiveChunkLocked()
    {
        if (_activeChunkPersistedCount >= _activeChunk.Count)
        {
            return;
        }
        AppendLines(ChunkFile(_activeChunkSequence), _activeChunk.Skip(_activeChunkPersistedCount));
        _activeChunkPersistedCount = _activeChunk.Count;
    }

    // must be called while already holding `_lock`
    private void EvictExcessChunks()
    {
        while (_closedChunks.Count > _maxFiles)
        {
            var oldest = _closedChunks.First!.Value;
            _closedChunks.RemoveFirst();
            DeleteChunkFile(oldest);
            _internalLog?.Invoke($"Pending chunk limit ({_maxFiles} files) exceeded - dropped oldest chunk {oldest} (up to {_entriesPerFile} entries)");
        }
    }

    /// <summary>
    /// A batch of entries taken via <see cref="TakeBatch"/>: either a full closed chunk read from
    /// disk, or a snapshot of what the active chunk held (with how much of it, if any, was already
    /// durable at take time).
    /// </summary>
    public sealed class TakenBatch
    {
        public IReadOnlyList<string> Items { get; }
        public long Sequence { get; }
        public bool FromClosedChunk { get; }
        public int PersistedCount { get; }

        private TakenBatch(IReadOnlyList<string> items, long sequence, bool fromClosedChunk, int persistedCount)
        {
            Items = items;
            Sequence = sequence;
            FromClosedChunk = fromClosedChunk;
            PersistedCount = persistedCount;
        }

        public static TakenBatch Empty { get; } = new(Array.Empty<string>(), -1, false, 0);

        public static TakenBatch OfClosedChunk(IReadOnlyList<string> items, long sequence) => new(items, sequence, true, items.Count);

        public static TakenBatch OfActiveChunk(IReadOnlyList<string> items, long sequence, int persistedCount) => new(items, sequence, false, persistedCount);

        public bool IsEmpty => Items.Count == 0;
    }

    // --- Chunk file naming/discovery -----------------------------------------------------------

    private string? ChunkFile(long sequence)
    {
        return _walDir == null ? null : Path.Combine(_walDir, $"{sequence:D6}{ChunkFileSuffix}");
    }

    private void DeleteChunkFile(long sequence)
    {
        var file = ChunkFile(sequence);
        if (file == null || !File.Exists(file))
        {
            return;
        }

        try
        {
            File.Delete(file);
        }
        catch (Exception ex)
        {
            _internalLog?.Invoke($"Unable to delete {file}: {ex.Message}");
        }
    }

    private static List<long> ListChunkSequences(string? dir)
    {
        var sequences = new List<long>();
        if (dir == null || !Directory.Exists(dir))
        {
            return sequences;
        }

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var name = Path.GetFileName(file);
            if (!name.EndsWith(ChunkFileSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var stem = name.Substring(0, name.Length - ChunkFileSuffix.Length);
            if (long.TryParse(stem, out var sequence))
            {
                sequences.Add(sequence);
            }
        }

        sequences.Sort();
        return sequences;
    }

    // --- Plain single-file I/O helpers ---------------------------------------------------------

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
