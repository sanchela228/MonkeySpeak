using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Core.Application.Calls.Networking;

public sealed class UdpUnifiedManager : IDisposable
{
    public enum MessageType : byte
    {
        HolePunch = 0,
        Audio = 1,
        Control = 2,
        Ping = 3,
        Avatar = 4
    }

    public event Action<IPEndPoint, IPEndPoint>? OnConnected;
    public event Action<byte[]>? OnAudioData;
    public event Action<byte[]>? OnControlData;
    public event Action<byte[]>? OnHolePunchData;
    public event Action<byte[]>? OnPingData;

    public event Action<string, IPEndPoint, IPEndPoint>? OnInterlocutorConnected;
    public event Action<string, byte[]>? OnAudioDataByInterlocutor;
    public event Action<string, byte[]>? OnControlDataByInterlocutor;
    public event Action<string, byte[]>? OnHolePunchDataByInterlocutor;
    public event Action<string, byte[]>? OnPingDataByInterlocutor;

    public event Action<byte[]>? OnAvatarData;
    public event Action<string, byte[]>? OnAvatarDataByInterlocutor;

    private UdpClient? _client;
    private IPEndPoint? _remote;
    private CancellationTokenSource? _cts;
    private volatile bool _isConnected;

    private int _pingCounter;
    private long _totalReceivedPackets;
    private long _audioReceivedPackets;
    private long _unmappedAudioPackets;

    private readonly Dictionary<string, IPEndPoint> _interlocutorToRemote = new();
    private readonly Dictionary<IPEndPoint, string> _remoteToInterlocutor = new();
    private readonly Dictionary<string, bool> _interlocutorConnected = new();
    private readonly Dictionary<string, CancellationTokenSource> _interlocutorCts = new();

    public void StartWithClient(UdpClient client, IPEndPoint remote, CancellationToken ct)
    {
        Start(client, ct);
        _remote = remote;

        Task.Run(() => HolePunchLoopAsync(_cts!.Token));

        Core.Logger.Info($"UDP remote set: {_remote}");
    }

    public void Start(UdpClient client, CancellationToken ct)
    {
        _client = client;
        ConfigureClient(_client);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        Task.Run(() => ReceiveLoopAsync(_cts.Token));
        Task.Run(() => PingLoopAsync(_cts.Token));

        Core.Logger.Info($"UDP started. local={_client.Client.LocalEndPoint}");
    }

    public void AddInterlocutor(string interlocutorId, IPEndPoint remote)
    {
        if (_client == null)
            throw new InvalidOperationException("UDP not initialized");

        _interlocutorToRemote[interlocutorId] = remote;
        _remoteToInterlocutor[remote] = interlocutorId;
        _interlocutorConnected[interlocutorId] = _interlocutorConnected.TryGetValue(interlocutorId, out var c) && c;

        Core.Logger.Info($"UDP AddInterlocutor: {Short(interlocutorId)} -> {remote} (count={_interlocutorToRemote.Count})");

        if (_cts != null)
        {
            if (_interlocutorCts.TryGetValue(interlocutorId, out var oldCts))
            {
                try { oldCts.Cancel(); oldCts.Dispose(); } catch { }
            }

            var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            _interlocutorCts[interlocutorId] = linked;
            _ = Task.Run(() => HolePunchLoopForInterlocutorAsync(interlocutorId, remote, linked.Token));
        }
    }

    public void RemoveInterlocutor(string interlocutorId)
    {
        if (_interlocutorCts.TryGetValue(interlocutorId, out var cts))
        {
            try { cts.Cancel(); cts.Dispose(); } catch { }
            _interlocutorCts.Remove(interlocutorId);
        }

        if (_interlocutorToRemote.TryGetValue(interlocutorId, out var ep))
        {
            _remoteToInterlocutor.Remove(ep);
        }

        _interlocutorToRemote.Remove(interlocutorId);
        _interlocutorConnected.Remove(interlocutorId);

        Core.Logger.Info($"UDP RemoveInterlocutor: {Short(interlocutorId)} (count={_interlocutorToRemote.Count})");
    }

    public Task SendAudioAsync(ReadOnlyMemory<byte> payload) => SendAsync(MessageType.Audio, payload);
    public Task SendControlAsync(ReadOnlyMemory<byte> payload) => SendAsync(MessageType.Control, payload);
    public Task SendAvatarAsync(ReadOnlyMemory<byte> payload) => SendAsync(MessageType.Avatar, payload);

    public Task SendAvatarToInterlocutorAsync(string interlocutorId, ReadOnlyMemory<byte> payload)
    {
        if (!_interlocutorToRemote.TryGetValue(interlocutorId, out var ep))
            throw new InvalidOperationException("Unknown interlocutor");

        return SendToRemoteAsync(MessageType.Avatar, payload, ep);
    }

    public Task SendPingAsync() => SendAsync(MessageType.Ping, Encoding.UTF8.GetBytes("PING"));

    public Task SendPingToInterlocutorAsync(string interlocutorId)
    {
        if (!_interlocutorToRemote.TryGetValue(interlocutorId, out var ep))
            throw new InvalidOperationException("Unknown interlocutor");

        return SendToRemoteAsync(MessageType.Ping, Encoding.UTF8.GetBytes("PING"), ep);
    }

    public Task SendControlToInterlocutorAsync(string interlocutorId, ReadOnlyMemory<byte> payload)
    {
        if (!_interlocutorToRemote.TryGetValue(interlocutorId, out var ep))
            throw new InvalidOperationException("Unknown interlocutor");

        return SendToRemoteAsync(MessageType.Control, payload, ep);
    }

    public async Task SendAsync(MessageType type, ReadOnlyMemory<byte> payload)
    {
        if (_client == null)
            throw new InvalidOperationException("UDP not initialized");

        if (_interlocutorToRemote.Count > 0)
        {
            foreach (var kv in _interlocutorToRemote)
                await SendToRemoteAsync(type, payload, kv.Value).ConfigureAwait(false);

            return;
        }

        if (_remote != null)
        {
            await SendToRemoteAsync(type, payload, _remote).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException("UDP remote(s) not initialized");
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _cts?.Dispose(); } catch { }
        _cts = null;

        foreach (var kv in _interlocutorCts)
            try { kv.Value.Cancel(); kv.Value.Dispose(); } catch { }

        _interlocutorCts.Clear();
        _interlocutorToRemote.Clear();
        _remoteToInterlocutor.Clear();
        _interlocutorConnected.Clear();

        _remote = null;
        _isConnected = false;
        _pingCounter = 0;
        _totalReceivedPackets = 0;
        _audioReceivedPackets = 0;
        _unmappedAudioPackets = 0;

        Core.Logger.Debug("UDP stopped");
    }

    public void Dispose()
    {
        Stop();
        _client?.Dispose();
        _client = null;
    }

    private async Task SendToRemoteAsync(MessageType type, ReadOnlyMemory<byte> payload, IPEndPoint remote)
    {
        if (_client == null)
            throw new InvalidOperationException("UDP not initialized");

        var buf = new byte[1 + payload.Length];
        buf[0] = (byte)type;
        payload.CopyTo(buf.AsMemory(1));

        await _client.SendAsync(buf, buf.Length, remote).ConfigureAwait(false);
    }

    private static void ConfigureClient(UdpClient client)
    {
        client.Client.ReceiveTimeout = 1000;
        try
        {
            const int SIO_UDP_CONNRESET = -1744830452;
            client.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
        }
        catch
        {
        }
    }

    private async Task HolePunchLoopAsync(CancellationToken ct)
    {
        while (!_isConnected && !ct.IsCancellationRequested)
        {
            try
            {
                await SendAsync(MessageType.HolePunch, Encoding.UTF8.GetBytes("PING")).ConfigureAwait(false);
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
            catch
            {
                try { await Task.Delay(500, ct).ConfigureAwait(false); } catch { }
            }
        }
    }

    private async Task HolePunchLoopForInterlocutorAsync(string interlocutorId, IPEndPoint remote, CancellationToken ct)
    {
        while (!_interlocutorConnected.GetValueOrDefault(interlocutorId) && !ct.IsCancellationRequested)
        {
            try
            {
                await SendToRemoteAsync(MessageType.HolePunch, Encoding.UTF8.GetBytes("PING"), remote).ConfigureAwait(false);
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
            catch
            {
                try { await Task.Delay(500, ct).ConfigureAwait(false); } catch { }
            }
        }
    }

    private async Task PingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);

                if (_client == null || !HasAnyTargets())
                    continue;

                Interlocked.Increment(ref _pingCounter);
                await SendAsync(MessageType.Ping, Encoding.UTF8.GetBytes("PING")).ConfigureAwait(false);

                var pingNo = Volatile.Read(ref _pingCounter);
                if (pingNo <= 3 || pingNo % 10 == 0)
                    Core.Logger.Debug($"UDP ping sent #{pingNo}");
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch
            {
            }
        }

        Core.Logger.Debug("UDP ping loop exited");
    }

    private bool HasAnyTargets()
    {
        return _interlocutorToRemote.Count > 0 || _remote is not null;
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_client == null)
            return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _client.ReceiveAsync(ct).ConfigureAwait(false);
                var data = result.Buffer;
                if (data.Length == 0)
                    continue;

                var type = (MessageType)data[0];
                var payload = new byte[data.Length - 1];
                Buffer.BlockCopy(data, 1, payload, 0, payload.Length);

                var total = Interlocked.Increment(ref _totalReceivedPackets);
                if (total == 1)
                    Core.Logger.Info($"UDP first packet received: type={type} from={result.RemoteEndPoint} len={data.Length}");

                switch (type)
                {
                    case MessageType.HolePunch:
                        HandleHolePunch(result.RemoteEndPoint, payload);
                        break;

                    case MessageType.Audio:
                        var audioN = Interlocked.Increment(ref _audioReceivedPackets);
                        OnAudioData?.Invoke(payload);
                        if (_remoteToInterlocutor.TryGetValue(result.RemoteEndPoint, out var ilIdA))
                        {
                            OnAudioDataByInterlocutor?.Invoke(ilIdA, payload);
                        }
                        else
                        {
                            var unmapped = Interlocked.Increment(ref _unmappedAudioPackets);
                            if (unmapped <= 5 || unmapped % 100 == 0)
                                Core.Logger.Warn($"UDP audio from UNKNOWN endpoint {result.RemoteEndPoint} (unmapped={unmapped}, known=[{string.Join(",", _remoteToInterlocutor.Keys)}])");
                        }

                        if (audioN == 1 || audioN % 500 == 0)
                            Core.Logger.Debug($"UDP audio recv #{audioN}: from={result.RemoteEndPoint} len={payload.Length}");
                        break;

                    case MessageType.Control:
                        OnControlData?.Invoke(payload);
                        if (_remoteToInterlocutor.TryGetValue(result.RemoteEndPoint, out var ilIdC))
                            OnControlDataByInterlocutor?.Invoke(ilIdC, payload);
                        break;

                    case MessageType.Avatar:
                        OnAvatarData?.Invoke(payload);
                        if (_remoteToInterlocutor.TryGetValue(result.RemoteEndPoint, out var ilIdAv))
                            OnAvatarDataByInterlocutor?.Invoke(ilIdAv, payload);
                        break;

                    case MessageType.Ping:
                        await HandlePingAsync(result.RemoteEndPoint, payload).ConfigureAwait(false);
                        break;
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch
            {
                try { await Task.Delay(200, ct).ConfigureAwait(false); } catch { }
            }
        }

        Core.Logger.Debug("UDP receive loop exited");
    }

    private void HandleHolePunch(IPEndPoint remoteEndPoint, byte[] payload)
    {
        if (_client == null)
            return;

        Core.Logger.Debug($"UDP HolePunch from {remoteEndPoint}, text={Encoding.UTF8.GetString(payload)}");

        if (!_isConnected && _remote != null && remoteEndPoint.Equals(_remote))
        {
            _isConnected = true;
            Core.Logger.Info($"UDP hole punch SUCCESS (legacy mode): {remoteEndPoint}");
            OnConnected?.Invoke((IPEndPoint)_client.Client.LocalEndPoint!, _remote);
        }

        if (_remoteToInterlocutor.TryGetValue(remoteEndPoint, out var ilId))
        {
            if (!_interlocutorConnected.GetValueOrDefault(ilId))
            {
                _interlocutorConnected[ilId] = true;
                Core.Logger.Info($"UDP interlocutor connected: {Short(ilId)} @ {remoteEndPoint}");
                OnInterlocutorConnected?.Invoke(ilId, (IPEndPoint)_client.Client.LocalEndPoint!, remoteEndPoint);
            }
        }
        else
        {
            foreach (var kvp in _interlocutorToRemote)
            {
                if (kvp.Value.Address.Equals(remoteEndPoint.Address))
                {
                    var oldRemote = kvp.Value;
                    _remoteToInterlocutor.Remove(oldRemote, out _);
                    _interlocutorToRemote[kvp.Key] = remoteEndPoint;
                    _remoteToInterlocutor[remoteEndPoint] = kvp.Key;
                    Core.Logger.Info($"UDP port remapped: {Short(kvp.Key)} {oldRemote} -> {remoteEndPoint}");

                    if (!_interlocutorConnected.GetValueOrDefault(kvp.Key))
                    {
                        _interlocutorConnected[kvp.Key] = true;
                        OnInterlocutorConnected?.Invoke(kvp.Key, (IPEndPoint)_client.Client.LocalEndPoint!, remoteEndPoint);
                    }

                    break;
                }
            }
        }

        var text = Encoding.UTF8.GetString(payload);
        if (text == "PING")
        {
            var pong = Encoding.UTF8.GetBytes("PONG");
            if (_remoteToInterlocutor.TryGetValue(remoteEndPoint, out var ilId2) &&
                _interlocutorToRemote.TryGetValue(ilId2, out var ep))
            {
                _ = SendToRemoteAsync(MessageType.HolePunch, pong, ep);
            }
            else
            {
                _ = SendAsync(MessageType.HolePunch, pong);
            }
        }

        OnHolePunchData?.Invoke(payload);
        if (_remoteToInterlocutor.TryGetValue(remoteEndPoint, out var ilId3))
            OnHolePunchDataByInterlocutor?.Invoke(ilId3, payload);
    }

    private async Task HandlePingAsync(IPEndPoint remoteEndPoint, byte[] payload)
    {
        OnPingData?.Invoke(payload);
        if (_remoteToInterlocutor.TryGetValue(remoteEndPoint, out var ilId))
            OnPingDataByInterlocutor?.Invoke(ilId, payload);

        var text = Encoding.UTF8.GetString(payload);
        if (text == "PING")
        {
            try
            {
                Core.Logger.Debug($"UDP ping received from {remoteEndPoint}");
                await SendToRemoteAsync(MessageType.Ping, Encoding.UTF8.GetBytes("PONG"), remoteEndPoint).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private static string Short(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "<empty>";

        return id.Length <= 8 ? id : id[..8];
    }
}
