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
}