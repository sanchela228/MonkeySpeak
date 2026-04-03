using System.Security.Cryptography;
using System.Text;
using Core.Database.Models;
using Microsoft.EntityFrameworkCore;
using NSec.Cryptography;

namespace Core.Database.Services;

public class UserService
{
    private readonly Context _context;

    public UserService(Context context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public Task<List<User>> GetUsersByUsernameAsync(string username)
    {
        return _context.Users
            .Where(u => u.Username.ToLower() == username.ToLower())
            .ToListAsync();
    }

    public Task<User?> GetUserByHandleAsync(string username, string userCode)
    {
        return _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower()
                                   && u.UserCode.ToLower() == userCode.ToLower());
    }

    public Task<User?> GetUserByUserCodeAsync(string userCode)
    {
        return _context.Users
            .FirstOrDefaultAsync(u => u.UserCode == userCode);
    }

    public async Task<User> CreateUserAsync(string username, byte[] publicKeyEd25519, byte[] publicKeyX25519)
    {
        var fingerprint = ComputeFingerprint(publicKeyEd25519);

        var user = new User
        {
            Username = username,
            UserCode = await GenerateUniqueUserCodeAsync(),
            PublicKeyEd25519 = publicKeyEd25519,
            PublicKeyX25519 = publicKeyX25519,
            KeyFingerprint = fingerprint,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task UpdateLastSeenAsync(Guid userId)
    {
        var user = await GetUserByIdAsync(userId);
        if (user != null)
        {
            user.LastSeenAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<(byte[] PublicKeyEd25519, byte[] PublicKeyX25519)> GetPublicKeysAsync(Guid userId)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' not found");
        }

        return (user.PublicKeyEd25519, user.PublicKeyX25519);
    }

    public bool VerifySignature(byte[] publicKeyEd25519, byte[] data, byte[] signature)
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            var publicKey = PublicKey.Import(algorithm, publicKeyEd25519, KeyBlobFormat.RawPublicKey);
            return algorithm.Verify(publicKey, data, signature);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> VerifySignatureAsync(Guid userId, byte[] nonce, byte[] signature)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        return VerifySignature(user.PublicKeyEd25519, nonce, signature);
    }

    public string ComputeFingerprint(byte[] publicKey)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(publicKey);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<User> CreateUserWithPasswordAsync(string username, string password)
    {
        var user = new User
        {
            Username = username,
            UserCode = await GenerateUniqueUserCodeAsync(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            PublicKeyEd25519 = Array.Empty<byte>(),
            PublicKeyX25519 = Array.Empty<byte>(),
            KeyFingerprint = string.Empty,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User?> VerifyPasswordAsync(string username, string password)
    {
        var users = await _context.Users
            .Where(u => u.Username == username && u.PasswordHash != null && u.PasswordHash != string.Empty)
            .ToListAsync();

        foreach (var user in users)
        {
            if (string.IsNullOrEmpty(user.PasswordHash))
                continue;

            if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return user;
        }

        return null;
    }

    private async Task<string> GenerateUniqueUserCodeAsync()
    {
        const string chars = "0123456789abcdef";
        var rng = new Random();

        while (true)
        {
            var code = new string(Enumerable.Range(0, 4).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
            var exists = await _context.Users.AnyAsync(u => u.UserCode == code);
            if (!exists)
                return code;
        }
    }

    public async Task<User> UpdateUsernameAsync(Guid userId, string newUsername)
    {
        if (string.IsNullOrWhiteSpace(newUsername))
            throw new ArgumentException("Username cannot be empty");

        newUsername = newUsername.Trim();
        if (newUsername.Length < 2 || newUsername.Length > 50)
            throw new ArgumentException("Username must be between 2 and 50 characters");

        var user = await _context.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found");

        user.Username = newUsername;
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<List<User>> SearchUsersAsync(string query, int limit = 20)
    {
        return await _context.Users
            .Where(u => u.Username.Contains(query))
            .Take(limit)
            .ToListAsync();
    }
}
