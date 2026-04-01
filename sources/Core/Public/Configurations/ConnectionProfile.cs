namespace Core.Public.Configurations;

public sealed record ConnectionProfile(
    string Id,
    string Name,
    string Domain,
    int Port,
    bool UseSsl,
    int MaxRetries)
{
    public string ConnectionString => $"{Domain}:{Port}";
};
