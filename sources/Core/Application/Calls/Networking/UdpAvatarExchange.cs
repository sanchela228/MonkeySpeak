using System.Security.Cryptography;

namespace Core.Application.Calls.Networking;

public sealed class UdpAvatarExchange
{
    private enum SubType : byte
    {
        Hash = 0x00,
        Request = 0x01,
        DataChunk = 0x02
    }

    private const int HashSize = 16;
    private const int MaxAvatarBytes = 65_536;
    private const int ChunkPayloadSize = 1200;
    private static readonly TimeSpan AssemblyTimeout = TimeSpan.FromSeconds(10);
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];

    public event Action<string, byte[]>? OnRemoteAvatarHash;
    public event Action<string>? OnRemoteAvatarRequested;
    public event Action<string, byte[]>? OnRemoteAvatarReceived;

    private UdpUnifiedManager? _udp;
    private bool _attached;

    private readonly Dictionary<string, ChunkAssembly> _pending = new();

    public void Attach(UdpUnifiedManager udp)
    {
        if (udp == null) throw new ArgumentNullException(nameof(udp));
        if (_attached && _udp == udp) return;
        Detach();

        _udp = udp;
        _udp.OnAvatarDataByInterlocutor += HandleAvatarData;
        _attached = true;
    }

    public void Detach()
    {
        try
        {
            if (_attached && _udp != null)
                _udp.OnAvatarDataByInterlocutor -= HandleAvatarData;
        }
        catch { }
        finally
        {
            _attached = false;
            _udp = null;
            _pending.Clear();
        }
    }

    public void SendHash(byte[] avatarHash, string interlocutorId)
    {
        if (_udp == null) return;

        var buf = new byte[1 + HashSize];
        buf[0] = (byte)SubType.Hash;
        var src = avatarHash.Length >= HashSize ? avatarHash.AsSpan(0, HashSize) : avatarHash.AsSpan();
        src.CopyTo(buf.AsSpan(1));

        try { _ = _udp.SendAvatarToInterlocutorAsync(interlocutorId, buf); } catch { }
    }

    public void SendEmptyHash(string interlocutorId)
    {
        SendHash(new byte[HashSize], interlocutorId);
    }

    public void SendRequest(string interlocutorId)
    {
        if (_udp == null) return;

        try { _ = _udp.SendAvatarToInterlocutorAsync(interlocutorId, new byte[] { (byte)SubType.Request }); } catch { }
    }

    public void SendAvatar(byte[] jpegBytes, string interlocutorId)
    {
        if (_udp == null || jpegBytes.Length == 0 || jpegBytes.Length > MaxAvatarBytes) return;

        var totalChunks = (jpegBytes.Length + ChunkPayloadSize - 1) / ChunkPayloadSize;

        for (var i = 0; i < totalChunks; i++)
        {
            var offset = i * ChunkPayloadSize;
            var len = Math.Min(ChunkPayloadSize, jpegBytes.Length - offset);

            var buf = new byte[1 + 2 + 2 + len];
            buf[0] = (byte)SubType.DataChunk;
            buf[1] = (byte)(i >> 8);
            buf[2] = (byte)(i & 0xFF);
            buf[3] = (byte)(totalChunks >> 8);
            buf[4] = (byte)(totalChunks & 0xFF);
            Buffer.BlockCopy(jpegBytes, offset, buf, 5, len);

            try { _ = _udp.SendAvatarToInterlocutorAsync(interlocutorId, buf); } catch { }
        }

        Core.Logger.Debug($"Avatar sent to {interlocutorId}: {jpegBytes.Length} bytes in {totalChunks} chunks");
    }

    public static byte[] ComputeHash(byte[] data)
    {
        var full = SHA256.HashData(data);
        var result = new byte[HashSize];
        Buffer.BlockCopy(full, 0, result, 0, HashSize);
        return result;
    }

    private void HandleAvatarData(string interlocutorId, byte[] data)
    {
        try
        {
            if (data.Length < 1) return;

            var subType = (SubType)data[0];
            var payload = data.AsSpan(1);

            switch (subType)
            {
                case SubType.Hash:
                    if (payload.Length >= HashSize)
                    {
                        var hash = payload.Slice(0, HashSize).ToArray();
                        OnRemoteAvatarHash?.Invoke(interlocutorId, hash);
                    }
                    break;

                case SubType.Request:
                    OnRemoteAvatarRequested?.Invoke(interlocutorId);
                    break;

                case SubType.DataChunk:
                    HandleChunk(interlocutorId, payload);
                    break;
            }
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"UdpAvatarExchange.Handle error: {ex.Message}");
        }
    }

    private void HandleChunk(string interlocutorId, ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4) return;

        var chunkIndex = (payload[0] << 8) | payload[1];
        var totalChunks = (payload[2] << 8) | payload[3];
        var chunkData = payload.Slice(4);

        if (totalChunks <= 0 || totalChunks > (MaxAvatarBytes / ChunkPayloadSize + 1))
        {
            Core.Logger.Warn($"Avatar chunk rejected: invalid totalChunks={totalChunks}");
            return;
        }

        if (chunkIndex < 0 || chunkIndex >= totalChunks) return;

        if (!_pending.TryGetValue(interlocutorId, out var assembly))
        {
            assembly = new ChunkAssembly(totalChunks);
            _pending[interlocutorId] = assembly;
        }

        if (assembly.IsExpired)
        {
            assembly = new ChunkAssembly(totalChunks);
            _pending[interlocutorId] = assembly;
        }

        assembly.SetChunk(chunkIndex, chunkData);

        if (!assembly.IsComplete) return;

        _pending.Remove(interlocutorId);

        var fullData = assembly.Assemble();

        if (fullData.Length > MaxAvatarBytes)
        {
            Core.Logger.Warn($"Avatar rejected: size {fullData.Length} exceeds limit");
            return;
        }

        if (fullData.Length < 3 || fullData[0] != JpegMagic[0] || fullData[1] != JpegMagic[1] || fullData[2] != JpegMagic[2])
        {
            Core.Logger.Warn("Avatar rejected: invalid JPEG header");
            return;
        }

        Core.Logger.Info($"Avatar received from {interlocutorId}: {fullData.Length} bytes");
        OnRemoteAvatarReceived?.Invoke(interlocutorId, fullData);
    }

    private sealed class ChunkAssembly
    {
        private readonly byte[][] _chunks;
        private readonly bool[] _received;
        private readonly DateTime _created = DateTime.UtcNow;
        private int _receivedCount;

        public ChunkAssembly(int totalChunks)
        {
            _chunks = new byte[totalChunks][];
            _received = new bool[totalChunks];
        }

        public bool IsExpired => DateTime.UtcNow - _created > AssemblyTimeout;
        public bool IsComplete => _receivedCount == _chunks.Length;

        public void SetChunk(int index, ReadOnlySpan<byte> data)
        {
            if (_received[index]) return;
            _chunks[index] = data.ToArray();
            _received[index] = true;
            _receivedCount++;
        }

        public byte[] Assemble()
        {
            var totalLen = 0;
            foreach (var c in _chunks) totalLen += c.Length;

            var result = new byte[totalLen];
            var offset = 0;
            foreach (var c in _chunks)
            {
                Buffer.BlockCopy(c, 0, result, offset, c.Length);
                offset += c.Length;
            }

            return result;
        }
    }
}
