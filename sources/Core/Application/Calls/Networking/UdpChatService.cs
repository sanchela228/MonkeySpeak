using System;
using System.Text;

namespace Core.Application.Calls.Networking;

public sealed class ChatMessage
{
    public required string SenderId { get; init; }
    public required string SenderName { get; init; }
    public required string Text { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public bool IsLocal { get; init; }
}

public sealed class UdpChatService
{
    private UdpUnifiedManager? _udp;
    private bool _attached;

    public event Action<string, ChatMessage>? OnChatMessageReceived;

    public void Attach(UdpUnifiedManager udp)
    {
        if (udp == null) throw new ArgumentNullException(nameof(udp));
        if (_attached && _udp == udp) return;
        Detach();

        _udp = udp;
        _udp.OnChatDataByInterlocutor += HandleChatData;
        _attached = true;
    }

    public void Detach()
    {
        try
        {
            if (_attached && _udp != null)
                _udp.OnChatDataByInterlocutor -= HandleChatData;
        }
        catch { }
        finally
        {
            _attached = false;
            _udp = null;
        }
    }

    public void SendMessage(string senderName, string text)
    {
        if (_udp == null || string.IsNullOrEmpty(text)) return;
        var payload = Encode(senderName ?? "", text);
        _ = _udp.SendChatAsync(payload);
    }

    private static byte[] Encode(string senderName, string text)
    {
        var ts = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nameBytes = Encoding.UTF8.GetBytes(senderName);
        var nameLen = (byte)Math.Min(nameBytes.Length, 255);
        var textBytes = Encoding.UTF8.GetBytes(text);

        var buf = new byte[4 + 1 + nameLen + textBytes.Length];
        buf[0] = (byte)(ts >> 24);
        buf[1] = (byte)(ts >> 16);
        buf[2] = (byte)(ts >> 8);
        buf[3] = (byte)ts;
        buf[4] = nameLen;
        Buffer.BlockCopy(nameBytes, 0, buf, 5, nameLen);
        Buffer.BlockCopy(textBytes, 0, buf, 5 + nameLen, textBytes.Length);
        return buf;
    }

    private void HandleChatData(string interlocutorId, byte[] data)
    {
        try
        {
            if (data == null || data.Length < 6) return;

            var ts = ((uint)data[0] << 24) | ((uint)data[1] << 16) | ((uint)data[2] << 8) | data[3];
            var nameLen = data[4];
            if (data.Length < 5 + nameLen + 1) return;

            var name = Encoding.UTF8.GetString(data, 5, nameLen);
            var text = Encoding.UTF8.GetString(data, 5 + nameLen, data.Length - 5 - nameLen);

            var msg = new ChatMessage
            {
                SenderId = interlocutorId,
                SenderName = name,
                Text = text,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(ts),
                IsLocal = false
            };

            OnChatMessageReceived?.Invoke(interlocutorId, msg);
        }
        catch { }
    }
}
