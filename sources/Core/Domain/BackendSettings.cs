using Core.Public.Configurations;

namespace Core.Domain;

public sealed class BackendSettings : IAuthConfiguration
{
    public AuthMode AuthMode { get; set; } = AuthMode.KeyPair;
}
