namespace CodeTitans.Odysseus;

public sealed class OdysseusClient
{
    private readonly string _appId;
    private readonly string _appKey;

    public OdysseusClient(string appId, string appKey, string? userId = null, Guid? sessionId = null)
    {
        _appId = appId;
        _appKey = appKey;
    }

    public string? UserId
    {
        get;
        set;
    }

    public Guid? SessionId
    {
        get;
        set;
    }


}
