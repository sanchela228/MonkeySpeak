using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Engine.Managers;

public static class Cache
{
    private static string _cacheDirectory = AppContext.BaseDirectory;
    private static readonly Dictionary<string, CacheEntry> _cache = new();
    private static bool _initialized;

    public static void Init(string cacheDir)
    {
        _cacheDirectory = cacheDir;
        Directory.CreateDirectory(_cacheDirectory);
        _initialized = true;
    }

    public static void Set(string key, string value, TimeSpan? ttl = null)
    {
        EnsureInit();

        var entry = new CacheEntry
        {
            ExpireAt = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : null,
            Data = Encoding.UTF8.GetBytes(value)
        };

        _cache[key] = entry;
        File.WriteAllBytes(GetPath(key), Serialize(entry));
    }

    public static string? Get(string key, bool permanent = false)
    {
        EnsureInit();

        // memory
        if (_cache.TryGetValue(key, out var entry))
        {
            if (!permanent && IsExpired(entry))
            {
                Remove(key, permanent);
                return null;
            }

            return Encoding.UTF8.GetString(entry.Data);
        }

        // file
        var path = GetPath(key, permanent);
        if (!File.Exists(path))
            return null;

        entry = Deserialize(File.ReadAllBytes(path));
        if (!permanent && IsExpired(entry))
        {
            Remove(key, permanent);
            return null;
        }

        _cache[key] = entry;
        return Encoding.UTF8.GetString(entry.Data);
    }

    public static bool ExistsPermanent(string key) => Exists(key, true);
    
    public static bool ExistsFast(string key, bool permanent = false)
    {
        EnsureInit();
        return File.Exists(GetPath(key, permanent));
    }
    
    public static bool ExistsFastPermanent(string key)
    {
        EnsureInit();
        return File.Exists(GetPath(key, true));
    }
    
    public static bool Exists(string key, bool permanent = false)
    {
        EnsureInit();

        // memory
        if (_cache.TryGetValue(key, out var entry))
            return !IsExpired(entry);

        // file
        var path = GetPath(key, permanent);
        if (!File.Exists(path))
            return false;

        try
        {
            var entryFromFile = Deserialize(File.ReadAllBytes(path));
            if (!permanent && IsExpired(entryFromFile))
            {
                Remove(key, permanent);
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void SetPermanent(string key, string value)
    {
        Set(key, value, ttl: null);
    }
    
    public static void Remove(string key, bool permanent = false)
    {
        _cache.Remove(key);

        var path = GetPath(key, permanent);
        if (File.Exists(path))
            File.Delete(path);
    }
    
    public static FileStream OpenRead(string key, bool permanent = false)
    {
        EnsureInit();

        var dir = permanent
            ? Path.Combine(_cacheDirectory, "permanent")
            : Path.Combine(_cacheDirectory, "temp");

        var path = Path.Combine(dir, Hash(key) + ".bin");

        if (!File.Exists(path))
            throw new FileNotFoundException(path);

        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
    }
    
    public static FileStream OpenWrite(string key, bool permanent = false)
    {
        EnsureInit();

        var dir = permanent
            ? Path.Combine(_cacheDirectory, "permanent")
            : Path.Combine(_cacheDirectory, "temp");

        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, Hash(key) + ".bin");

        return new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None
        );
    }
    
    public static string GetPath(string key, bool permanent = false)
    {
        var dir = permanent
            ? Path.Combine(_cacheDirectory, "permanent")
            : Path.Combine(_cacheDirectory, "temp");

        return Path.Combine(dir, Hash(key) + ".bin");
    }
    
    #region Internals

    private static void EnsureInit()
    {
        if (!_initialized)
            throw new InvalidOperationException("Cache.Init() was not called");
    }

    private static bool IsExpired(CacheEntry entry)
    {
        return entry.ExpireAt.HasValue && entry.ExpireAt.Value <= DateTime.UtcNow;
    }

    private static string GetPath(string key)
    {
        return Path.Combine(_cacheDirectory, Hash(key) + ".cache");
    }

    private static byte[] Serialize(CacheEntry entry)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(entry.ExpireAt?.ToBinary() ?? 0L);
        bw.Write(entry.Data.Length);
        bw.Write(entry.Data);

        return ms.ToArray();
    }

    private static CacheEntry Deserialize(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);

        var ticks = br.ReadInt64();
        var len = br.ReadInt32();

        return new CacheEntry
        {
            ExpireAt = ticks == 0 ? null : DateTime.FromBinary(ticks),
            Data = br.ReadBytes(len)
        };
    }

    private static string Hash(string input)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(input)));
    }

    #endregion
}

internal sealed class CacheEntry
{
    public DateTime? ExpireAt { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
