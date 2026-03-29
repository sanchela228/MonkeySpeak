namespace Core.Configurations;

public class BackendSettings
{
    public AuthMode AuthMode { get; set; } = AuthMode.KeyPair;
}

public enum AuthMode
{
    KeyPair,
    Password
}
