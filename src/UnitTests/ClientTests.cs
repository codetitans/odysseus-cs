using CodeTitans.Odysseus;

namespace UnitTests;

public sealed class ClientTests
{
    [Fact]
    public void create_client_with_success()
    {
        var client = new OdysseusClient("1", "1");
        Assert.NotNull(client);
    }

    [Fact]
    public void create_client_with_exception_on_no_input()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            var client = new OdysseusClient(null, null);
            Assert.NotNull(client);
        });
    }
}
