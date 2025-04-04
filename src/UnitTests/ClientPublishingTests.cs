using CodeTitans.Odysseus;

namespace UnitTests;

public sealed class ClientPublishingTests
{
    [Fact]
    public async Task publish_simple_logs_instantly()
    {
        var client = new OdysseusClient("app_052c18d7d5154fd1b945b8173bf9eff2", "0M18_pCf5msaqu7Zd6b0C3lD", delay: 0);

        client.Log("Test message");
        await Task.Delay(1000);
    }

    [Fact]
    public async Task publish_multiple_messages_at_once()
    {
        var client = new OdysseusClient("app_052c18d7d5154fd1b945b8173bf9eff2", "0M18_pCf5msaqu7Zd6b0C3lD", delay: 0);
        var ts = DateTime.UtcNow;
        var tag = "T1'";

        client.SessionId = Guid.Parse("fd416bad-beef-418e-96c1-74a27dc4196e");
        client.Log("Message-1", tag: tag, timestamp: ts);
        client.Log("Message-2", tag: tag, timestamp: ts);
        client.Log("Message-3", tag: tag, timestamp: ts);
        await Task.Delay(1000);
    }
}
