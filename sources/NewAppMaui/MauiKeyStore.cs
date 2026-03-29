using Core.Application.Abstractions;
using Core.Application.Crypto;

namespace NewAppMaui;

public class MauiKeyStore : IKeyStore
{
    private const string UserIdKey = "auth.userId";
    private const string UsernameKey = "auth.username";
    private const string FingerprintKey = "auth.fingerprint";
    private const string EdPrivFileName = "ed25519.priv.bin";
    private const string EdPubFileName = "ed25519.pub.bin";
    private const string XPubFileName = "x25519.pub.bin";

    private static string KeyDir => FileSystem.AppDataDirectory;

    public Task<string?> GetUserIdAsync()
        => Task.FromResult<string?>(Preferences.Get(UserIdKey, null as string));

    public Task<string> GetOrCreateUsernameAsync()
    {
        var existing = Preferences.Get(UsernameKey, null as string);
        if (!string.IsNullOrEmpty(existing))
            return Task.FromResult(existing);

        var generated = "user_" + Guid.NewGuid().ToString("N")[..8];
        Preferences.Set(UsernameKey, generated);
        return Task.FromResult(generated);
    }

    public Task SaveRegistrationAsync(string userId, string username, string fingerprint)
    {
        Preferences.Set(UserIdKey, userId);
        Preferences.Set(UsernameKey, username);
        Preferences.Set(FingerprintKey, fingerprint);
        return Task.CompletedTask;
    }

    public Task<bool> HasKeysAsync()
    {
        var privPath = Path.Combine(KeyDir, EdPrivFileName);
        return Task.FromResult(File.Exists(privPath));
    }

    public async Task<(byte[] privateEd25519, byte[] publicEd25519, byte[] publicX25519)> GetOrCreateKeysAsync()
    {
        var privEdPath = Path.Combine(KeyDir, EdPrivFileName);
        var pubEdPath = Path.Combine(KeyDir, EdPubFileName);
        var pubXPath = Path.Combine(KeyDir, XPubFileName);

        if (File.Exists(privEdPath) && File.Exists(pubEdPath) && File.Exists(pubXPath))
        {
            var privEd = await File.ReadAllBytesAsync(privEdPath);
            var pubEd = await File.ReadAllBytesAsync(pubEdPath);
            var pubX = await File.ReadAllBytesAsync(pubXPath);
            return (privEd, pubEd, pubX);
        }

        var (newPrivEd, newPubEd, _, newPubX) = UserCrypto.GenerateKeys();
        await File.WriteAllBytesAsync(privEdPath, newPrivEd);
        await File.WriteAllBytesAsync(pubEdPath, newPubEd);
        await File.WriteAllBytesAsync(pubXPath, newPubX);
        return (newPrivEd, newPubEd, newPubX);
    }

    public Task ClearRegistrationAsync()
    {
        Preferences.Remove(UserIdKey);
        Preferences.Remove(FingerprintKey);
        // Keep UsernameKey so the user keeps their generated name
        return Task.CompletedTask;
    }
}
