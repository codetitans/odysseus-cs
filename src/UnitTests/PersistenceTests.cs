using CodeTitans.Odysseus;

namespace UnitTests;

public sealed class PersistenceTests
{
    // targetHost points at a port nobody is listening on, so every upload attempt fails fast
    // with "connection refused" - forcing the store into its persist/requeue path. A large delay
    // keeps the background upload timer from firing during the deterministic (chunking/eviction)
    // tests below, so their assertions only ever observe the synchronous effects of Add()/Log().
    private const string UnreachableHost = "http://127.0.0.1:1";
    private const int NoFlushDuringTestDelaySeconds = 60;

    [Fact]
    public void closes_a_chunk_file_once_entries_per_file_is_reached()
    {
        var dir = CreateTempDir();
        try
        {
            var client = new OdysseusClient("1", "1", delay: NoFlushDuringTestDelaySeconds, targetHost: UnreachableHost,
                storageDirectory: dir, entriesPerFile: 2, maxFiles: 100);
            var logsDir = Path.Combine(dir, "logs");

            client.Log("Message-0");
            Assert.False(Directory.Exists(logsDir), "active chunk below entriesPerFile should stay in memory");

            client.Log("Message-1");
            Assert.True(File.Exists(Path.Combine(logsDir, "000000.jsonl")));

            client.Log("Message-2");
            // still only one closed chunk on disk - Message-2 is sitting in the new active chunk, in memory
            Assert.Equal(new[] { "000000.jsonl" }, Directory.GetFiles(logsDir).Select(Path.GetFileName).Order());

            var content = File.ReadAllText(Path.Combine(logsDir, "000000.jsonl"));
            Assert.Contains("Message-0", content);
            Assert.Contains("Message-1", content);
            Assert.DoesNotContain("Message-2", content);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void drops_the_oldest_chunk_file_once_max_files_is_exceeded()
    {
        var dir = CreateTempDir();
        try
        {
            var client = new OdysseusClient("1", "1", delay: NoFlushDuringTestDelaySeconds, targetHost: UnreachableHost,
                storageDirectory: dir, entriesPerFile: 2, maxFiles: 1);
            var logsDir = Path.Combine(dir, "logs");

            for (var i = 0; i < 6; i++)
            {
                client.Log($"Message-{i}");
            }

            // entriesPerFile=2, maxFiles=1: three chunks get closed (000000..000002), each closure
            // evicting the previous one - only the newest closed chunk should remain on disk.
            Assert.Equal(new[] { "000002.jsonl" }, Directory.GetFiles(logsDir).Select(Path.GetFileName).Order());

            var content = File.ReadAllText(Path.Combine(logsDir, "000002.jsonl"));
            Assert.Contains("Message-4", content);
            Assert.Contains("Message-5", content);
            Assert.DoesNotContain("Message-0", content);
            Assert.DoesNotContain("Message-3", content);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void recovers_and_continues_chunk_numbering_across_a_new_client_instance()
    {
        var dir = CreateTempDir();
        try
        {
            var first = new OdysseusClient("1", "1", delay: NoFlushDuringTestDelaySeconds, targetHost: UnreachableHost,
                storageDirectory: dir, entriesPerFile: 2, maxFiles: 100);
            first.Log("Message-0");
            first.Log("Message-1");

            var logsDir = Path.Combine(dir, "logs");
            Assert.True(File.Exists(Path.Combine(logsDir, "000000.jsonl")));

            // A brand-new client pointed at the same directory recovers chunk 000000 as already
            // closed and must continue numbering from 000001, never overwriting it.
            var second = new OdysseusClient("1", "1", delay: NoFlushDuringTestDelaySeconds, targetHost: UnreachableHost,
                storageDirectory: dir, entriesPerFile: 2, maxFiles: 100);
            second.Log("Message-2");
            second.Log("Message-3");

            Assert.Equal(new[] { "000000.jsonl", "000001.jsonl" }, Directory.GetFiles(logsDir).Select(Path.GetFileName).Order());
            Assert.Contains("Message-0", File.ReadAllText(Path.Combine(logsDir, "000000.jsonl")));
            Assert.Contains("Message-2", File.ReadAllText(Path.Combine(logsDir, "000001.jsonl")));
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public async Task persists_the_active_chunk_to_disk_when_an_upload_attempt_fails()
    {
        var dir = CreateTempDir();
        try
        {
            // entriesPerFile is deliberately never reached here - the only way this entry can end up
            // on disk is via the failed-upload Requeue() path, not the "chunk is full" rotation path.
            var client = new OdysseusClient("1", "1", delay: 0, targetHost: UnreachableHost, storageDirectory: dir);

            client.Log("Message-1");
            var logsDir = Path.Combine(dir, "logs");
            await WaitUntil(() => Directory.Exists(logsDir) && Directory.GetFiles(logsDir).Length > 0);

            var content = string.Join('\n', Directory.GetFiles(logsDir).Select(File.ReadAllText));
            Assert.Contains("Message-1", content);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "odysseus-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMillis = 5000)
    {
        var elapsed = 0;
        const int step = 50;
        while (!condition() && elapsed < timeoutMillis)
        {
            await Task.Delay(step);
            elapsed += step;
        }
    }
}
