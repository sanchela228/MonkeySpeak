namespace Core.Public.Configurations;

public interface IAuthConfiguration
{
    AuthMode AuthMode { get; }
}

public enum AuthMode
{
    KeyPair,
    Password
}
