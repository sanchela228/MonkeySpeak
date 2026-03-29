namespace Core.Application.Abstractions;

public interface IKeyStore
{
    Task<string?> GetUserIdAsync();
    Task<string> GetOrCreateUsernameAsync();
    Task SaveRegistrationAsync(string userId, string username, string fingerprint);

    Task<bool> HasKeysAsync();
    Task<(byte[] privateEd25519, byte[] publicEd25519, byte[] publicX25519)> GetOrCreateKeysAsync();
    Task ClearRegistrationAsync();
}
