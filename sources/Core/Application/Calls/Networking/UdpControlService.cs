using System;
using System.Text;

namespace Core.Application.Calls.Networking;

public enum ControlCode : byte
{
    Hangup = 0x00,
    MuteState = 0x01,
    DisplayName = 0x02,
}

public class UdpControlService
{
    private UdpUnifiedManager? _udp;
    private bool _attached;

    public event Action<ControlCode, ReadOnlyMemory<byte>>? OnControl;
    public event Action<bool>? OnRemoteMuteChanged;
    public event Action? OnRemoteHangup;

    public event Action<string, ControlCode, ReadOnlyMemory<byte>>? OnControlByInterlocutor;
    public event Action<string, bool>? OnRemoteMuteChangedByInterlocutor;
    public event Action<string>? OnRemoteHangupByInterlocutor;
    public event Action<string, string>? OnRemoteDisplayNameReceived;

    public void Attach(UdpUnifiedManager udp)
    {
        if (udp == null) throw new ArgumentNullException(nameof(udp));
        if (_attached && _udp == udp) return;
        Detach();

        _udp = udp;
        _udp.OnControlData += HandleControlData;
        _udp.OnControlDataByInterlocutor += HandleControlDataByInterlocutor;
        _attached = true;
    }

    public void Detach()
    {
        try
        {
            if (_attached && _udp != null)
            {
                _udp.OnControlData -= HandleControlData;
                _udp.OnControlDataByInterlocutor -= HandleControlDataByInterlocutor;
            }
        }
        catch { }
        finally
        {
            _attached = false;
            _udp = null;
        }
    }

    public void Send(ControlCode code, ReadOnlySpan<byte> payload)
    {
        try
        {
            if (_udp == null) return;
            var buf = new byte[1 + payload.Length];
            buf[0] = (byte)code;
            payload.CopyTo(buf.AsSpan(1));
            _ = _udp.SendControlAsync(buf);
        }
        catch { }
    }

    public void Send(ControlCode code, ReadOnlySpan<byte> payload, string interlocutorId)
    {
        try
        {
            if (_udp == null) return;
            var buf = new byte[1 + payload.Length];
            buf[0] = (byte)code;
            payload.CopyTo(buf.AsSpan(1));
            _ = _udp.SendControlToInterlocutorAsync(interlocutorId, buf);
        }
        catch { }
    }

    public void Send(ControlCode code, byte value)
    {
        Span<byte> payload = stackalloc byte[1];
        payload[0] = value;
        Send(code, payload);
    }

    public void SendMuteState(bool enabled)
    {
        Send(ControlCode.MuteState, (byte)(enabled ? 1 : 0));
    }

    public void SendMuteStateToInterlocutor(bool enabled, string interlocutorId)
    {
        Span<byte> payload = stackalloc byte[1];
        payload[0] = (byte)(enabled ? 1 : 0);
        Send(ControlCode.MuteState, payload, interlocutorId);
    }

    public void SendHangup()
    {
        Send(ControlCode.Hangup, ReadOnlySpan<byte>.Empty);
    }

    public void SendDisplayName(string name, string interlocutorId)
    {
        if (string.IsNullOrEmpty(name)) return;
        var payload = Encoding.UTF8.GetBytes(name);
        Send(ControlCode.DisplayName, payload, interlocutorId);
    }

    public void SendDisplayNameToAll(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        var payload = Encoding.UTF8.GetBytes(name);
        Send(ControlCode.DisplayName, payload);
    }

    private void HandleControlData(byte[] data)
    {
        try
        {
            if (data == null || data.Length < 1) return;
            var code = (ControlCode)data[0];
            var payload = data.AsMemory(1);

            OnControl?.Invoke(code, payload);

            Core.Logger.Debug($"HandleControlData: {code}, {payload}");

            switch (code)
            {
                case ControlCode.Hangup:
                    OnRemoteHangup?.Invoke();
                    break;
                case ControlCode.MuteState:
                    if (payload.Length >= 1)
                    {
                        bool remoteMicEnabled = payload.Span[0] == 1;
                        OnRemoteMuteChanged?.Invoke(!remoteMicEnabled);
                    }
                    break;
            }
        }
        catch { }
    }

    private void HandleControlDataByInterlocutor(string interlocutorId, byte[] data)
    {
        try
        {
            if (data == null || data.Length < 1) return;
            var code = (ControlCode)data[0];
            var payload = data.AsMemory(1);

            OnControlByInterlocutor?.Invoke(interlocutorId, code, payload);

            switch (code)
            {
                case ControlCode.Hangup:
                    OnRemoteHangupByInterlocutor?.Invoke(interlocutorId);
                    break;
                case ControlCode.MuteState:
                    if (payload.Length >= 1)
                    {
                        bool remoteMicEnabled = payload.Span[0] == 1;
                        OnRemoteMuteChangedByInterlocutor?.Invoke(interlocutorId, !remoteMicEnabled);
                    }
                    break;
                case ControlCode.DisplayName:
                    if (payload.Length > 0)
                    {
                        var name = Encoding.UTF8.GetString(payload.Span);
                        OnRemoteDisplayNameReceived?.Invoke(interlocutorId, name);
                    }
                    break;
            }
        }
        catch { }
    }
}