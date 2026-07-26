namespace CodeTitans.Odysseus;

public interface IOdysseusSession
{
    public string? User
    {
        get;
        set;
    }

    public Guid SessionId
    {
        get;
        set;
    }

    public LogSeverity MinSeverity
    {
        get;
        set;
    }

    public short? Platform
    {
        get;
        set;
    }
}