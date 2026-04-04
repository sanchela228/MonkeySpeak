using System.Security.Cryptography;
using Core.Application.Calls.Networking;
using Core.Public.Services;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace NewAppMaui.Services;

public sealed class AvatarCacheService : IAvatarProvider
{
    private const int MaxSize = 128;
    private const int MaxBytes = 65_536;

    private readonly IUserSettingsService _settings;
    private readonly string _cacheDir;

    private byte[]? _ownBytes;
    private byte[]? _ownHash;

    public event Action<string>? UserAvatarUpdated;

    public AvatarCacheService(IUserSettingsService settings)
    {
        _settings = settings;
        _cacheDir = Path.Combine(settings.ProfileDirectory, "avatar_cache");
        Directory.CreateDirectory(_cacheDir);
    }

    public byte[]? GetOwnAvatarBytes()
    {
        EnsureOwnAvatarPrepared();
        return _ownBytes;
    }

    public byte[]? GetOwnAvatarHash()
    {
        EnsureOwnAvatarPrepared();
        return _ownHash;
    }

    public byte[]? GetCachedHash(string interlocutorId)
    {
        var hashPath = GetHashPath(interlocutorId);
        if (!File.Exists(hashPath)) return null;

        try { return File.ReadAllBytes(hashPath); }
        catch { return null; }
    }

    public string? GetCachedAvatarPath(string interlocutorId)
    {
        var path = GetAvatarPath(interlocutorId);
        return File.Exists(path) ? path : null;
    }

    public void SaveRemoteAvatar(string interlocutorId, byte[] jpegData, byte[] hash)
    {
        try
        {
            File.WriteAllBytes(GetAvatarPath(interlocutorId), jpegData);
            File.WriteAllBytes(GetHashPath(interlocutorId), hash);
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"AvatarCache.Save failed: {ex.Message}");
        }
    }

    public string? GetAvatarPathForUser(string userId)
    {
        var path = GetAvatarPath($"user_{userId}");
        return File.Exists(path) ? path : null;
    }

    public void SaveAvatarForUser(string userId, byte[] jpegData)
    {
        try
        {
            var hash = UdpAvatarExchange.ComputeHash(jpegData);
            File.WriteAllBytes(GetAvatarPath($"user_{userId}"), jpegData);
            File.WriteAllBytes(GetHashPath($"user_{userId}"), hash);
            UserAvatarUpdated?.Invoke(userId);
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"AvatarCache.SaveForUser failed: {ex.Message}");
        }
    }

    public void InvalidateOwn()
    {
        _ownBytes = null;
        _ownHash = null;
    }

    private void EnsureOwnAvatarPrepared()
    {
        if (_ownBytes != null) return;

        try
        {
            if (string.IsNullOrEmpty(_settings.AvatarPath)) return;

            var fullPath = Path.Combine(_settings.ProfileDirectory, _settings.AvatarPath);
            if (!File.Exists(fullPath)) return;

            var raw = File.ReadAllBytes(fullPath);
            _ownBytes = ResizeToJpeg(raw);
            _ownHash = UdpAvatarExchange.ComputeHash(_ownBytes);
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"AvatarCache.PrepareOwn failed: {ex.Message}");
            _ownBytes = null;
            _ownHash = null;
        }
    }

    private static byte[] ResizeToJpeg(byte[] source)
    {
        try
        {
            using var ms = new MemoryStream(source);
            var image = PlatformImage.FromStream(ms);
            if (image == null) return source;

            if (image.Width > MaxSize || image.Height > MaxSize)
                image = image.Downsize(MaxSize, true);

            var result = image.AsBytes(ImageFormat.Jpeg, 0.75f);

            if (result.Length > MaxBytes)
                result = image.AsBytes(ImageFormat.Jpeg, 0.5f);

            return result.Length <= MaxBytes ? result : source;
        }
        catch
        {
            return source.Length <= MaxBytes ? source : [];
        }
    }

    private string GetAvatarPath(string id) => Path.Combine(_cacheDir, $"{SanitizeId(id)}.jpg");
    private string GetHashPath(string id) => Path.Combine(_cacheDir, $"{SanitizeId(id)}.hash");

    private static string SanitizeId(string id)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id));
        return Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
    }
}
