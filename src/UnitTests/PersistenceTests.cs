using CodeTitans.Odysseus;

namespace UnitTests;

public sealed class PersistenceTests
{
    // targetHost points at a port nobody is listening on, so every upload attempt fails fast
    // with "connection refused" - forcing the store into its persist/requeue path.
    private const string UnreachableHost = "http://127.0.0.1:1";

    [Fact]
    public async Task pending_entries_persist_to_disk_when_upload_fails()
    {
        var dir = CreateTempDir();
        try
        {
            var client = new OdysseusClient("1", "1", delay: 0, targetHost: UnreachableHost, storageDirectory: dir);

            client.Log("Message-1");
            await WaitUntil(() => File.Exists(Path.Combine(dir, "pending-logs.jsonl")));

            var lines = await File.ReadAllLinesAsync(Path.Combine(dir, "pending-logs.jsonl"));
            Assert.Contains(lines, l => l.Contains("Message-1"));
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public async Task recovers_pending_entries_from_a_previous_session()
    {
        var dir = CreateTempDir();
        try
        {
            var first = new OdysseusClient("1", "1", delay: 0, targetHost: UnreachableHost, storageDirectory: dir);
            first.Log("Recovered-Message");
            await WaitUntil(() => File.Exists(Path.Combine(dir, "pending-logs.jsonl")));

            // A brand-new client pointed at the same directory should pick the pending entry back up
            // and (since the host is still unreachable) keep it durably persisted.
            var second = new OdysseusClient("1", "1", delay: 0, targetHost: UnreachableHost, storageDirectory: dir);
            await Task.Delay(200);

            var lines = await File.ReadAllLinesAsync(Path.Combine(dir, "pending-logs.jsonl"));
            Assert.Contains(lines, l => l.Contains("Recovered-Message"));
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public async Task drops_oldest_entries_once_max_entries_is_exceeded()
    {
        var dir = CreateTempDir();
        try
        {
            const int maxEntries = 3;
            var client = new OdysseusClient("1", "1", delay: 0, targetHost: UnreachableHost, storageDirectory: dir, maxEntries: maxEntries);

            for (var i = 0; i < 5; i++)
            {
                client.Log($"Message-{i}");
            }

            await WaitUntil(() => File.Exists(Path.Combine(dir, "pending-logs.jsonl")));
            // give the store a moment to settle after the failed upload requeues everything
            await Task.Delay(300);

            var lines = await File.ReadAllLinesAsync(Path.Combine(dir, "pending-logs.jsonl"));
            Assert.True(lines.Length <= maxEntries, $"expected at most {maxEntries} persisted lines, found {lines.Length}");

            var content = string.Join('\n', lines);
            Assert.Contains("Message-4", content);
            Assert.DoesNotContain("Message-0", content);
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
