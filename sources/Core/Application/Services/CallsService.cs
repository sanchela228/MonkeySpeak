using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Core.Application.Calls.Infrastructure;
using Core.Application.Calls.Networking;
using Core.Domain.Calls;
using Core.Public.Services;
using Core.Websockets;
using Core.Websockets.Messages.NoAuthCall;

namespace Core.Application.Services;

public class CallsService : ICallsService
{
    private readonly IConnectionService _connection;
    private readonly IAudioService _audio;
    private readonly IAvatarProvider? _avatarProvider;
    private readonly IUserSettingsService? _userSettings;
    private readonly IAuthService? _auth;
    private readonly IStunClient _stun;
    private readonly UdpUnifiedManager _udp;
    private readonly UdpControlService _controls;
    private readonly UdpAvatarExchange _avatarExchange;
    private readonly UdpChatService _chat;
    private readonly object _sync = new();

    private CancellationTokenSource? _sessionCts;
    private UdpClient? _udpClient;

    public CallSession? Current { get; private set; }
    public event EventHandler<CallSession?>? CurrentChanged;
    public event EventHandler<CallState>? StateChanged;

    public event Action<string, byte[]>? AvatarReceived;
    public event Action<string, bool>? MuteStateChanged;
    public event Action<string, string>? DisplayNameReceived;
    public event Action<string, ChatMessage>? ChatMessageReceived;

    public CallsService(IConnectionService connection, IAudioService audio, IStunClient stun, UdpUnifiedManager udp,
        IAvatarProvider? avatarProvider = null, IUserSettingsService? userSettings = null, IAuthService? auth = null)
    {
        _connection = connection;
        _audio = audio;
        _stun = stun;
        _udp = udp;
        _avatarProvider = avatarProvider;
        _userSettings = userSettings;
        _auth = auth;
        _controls = new UdpControlService();
        _avatarExchange = new UdpAvatarExchange();
        _chat = new UdpChatService();

        _connection.MessageReceived += OnWsMessage;

        _audio.OnEncodedAudioReady += OnEncodedAudioReady;
        _udp.OnAudioDataByInterlocutor += OnUdpAudioReceived;

        _controls.OnRemoteHangupByInterlocutor += OnRemoteHangupByInterlocutor;
        _controls.OnRemoteMuteChangedByInterlocutor += OnRemoteMuteChangedByInterlocutor;
        _controls.OnRemoteDisplayNameReceived += OnRemoteDisplayNameReceived;

        _chat.OnChatMessageReceived += (id, msg) => ChatMessageReceived?.Invoke(id, msg);

        _avatarExchange.OnRemoteAvatarHash += OnRemoteAvatarHash;
        _avatarExchange.OnRemoteAvatarRequested += OnRemoteAvatarRequested;
        _avatarExchange.OnRemoteAvatarReceived += OnRemoteAvatarReceived;

        _audio.MicrophoneEnabledChanged += OnLocalMicrophoneEnabledChanged;
        _udp.OnInterlocutorConnected += OnInterlocutorUdpConnected;
    }

    private void OnInterlocutorUdpConnected(string interlocutorId, IPEndPoint local, IPEndPoint remote)
    {
        try
        {
            _controls.SendMuteStateToInterlocutor(_audio.IsMicrophoneEnabled, interlocutorId);
        }
        catch { }

        try
        {
            var name = GetLocalDisplayName();
            if (!string.IsNullOrEmpty(name))
                _controls.SendDisplayName(name, interlocutorId);
        }
        catch { }
    }

    private void OnLocalMicrophoneEnabledChanged(bool enabled)
    {
        try
        {
            _controls.SendMuteState(enabled);
        }
        catch
        {
        }
    }

    private long _sentAudioCount;
    private long _recvAudioCount;

    private void OnEncodedAudioReady(byte[] opusPacket)
    {
        var n = Interlocked.Increment(ref _sentAudioCount);
        if (n == 1)
            Core.Logger.Info($"[CallsService] First audio packet sent, len={opusPacket.Length}");

        _ = _udp.SendAudioAsync(new ReadOnlyMemory<byte>(opusPacket));
    }

    private void OnUdpAudioReceived(string interlocutorId, byte[] opusData)
    {
        var n = Interlocked.Increment(ref _recvAudioCount);
        if (n == 1)
            Core.Logger.Info($"[CallsService] First audio packet received from {interlocutorId}, len={opusData.Length}");

        _audio.HandleReceivedAudio(interlocutorId, opusData);
    }

    public Task<CallSession> CreateAsync(CancellationToken ct = default)
    {
        return CreateInternalAsync(ct);
    }

    public Task<CallSession> JoinAsync(string code, CancellationToken ct = default)
    {
        return JoinInternalAsync(code, ct);
    }

    public Task HangupAsync(CancellationToken ct = default)
    {
        return HangupInternalAsync(reason: "UserHangup", ct);
    }

    private async Task<CallSession> CreateInternalAsync(CancellationToken ct)
    {
        EnsureWsConnected();
        ResetSession();

        var session = new CallSession();
        session.TransitionTo(CallState.Idle);
        SetCurrent(session);

        try
        {
            var localPort = SelectLocalUdpPort();
            var localLanEp = GetLocalLanEndpoint(localPort);

#if DEBUG
            var publicEp = localLanEp;
            Core.Logger.Info($"STUN skipped (DEBUG): using LAN endpoint {publicEp}");
#else
            var publicEp = await _stun.GetPublicEndPointAsync(localPort, timeoutMs: 5000, ct).ConfigureAwait(false);
#endif

            session.SetLocal(localPort, publicEp, localLanEp);

            StartUdpIfNeeded(session);

            var ipEndPoint = publicEp?.ToString() ?? localLanEp?.ToString() ?? new IPEndPoint(IPAddress.Loopback, localPort).ToString();
            Core.Logger.Info($"Calls.Create: localPort={localPort} ipEndPoint='{ipEndPoint}'");

            var msg = new CreateSession { Value = string.Empty, IpEndPoint = ipEndPoint };
            var ctx = Context.Create(msg);
            var json = JsonSerializer.Serialize(ctx);

            var created = await AwaitMessageAsync<SessionCreated>(
                send: () => _connection.SendAsync(json, ct),
                predicate: _ => true,
                ct: ct).ConfigureAwait(false);

            session.SetRoomCode(created.Value);
            if (!string.IsNullOrWhiteSpace(created.SelfInterlocutorId))
            {
                var selfEp = publicEp ?? localLanEp ?? new IPEndPoint(IPAddress.Loopback, localPort);
                session.SetSelf(new Interlocutor(created.SelfInterlocutorId, selfEp, CallState.Connected));
            }

            Transition(session, CallState.Waiting);
            return session;
        }
        catch
        {
            ResetSession();
            throw;
        }
    }

    private async Task<CallSession> JoinInternalAsync(string code, CancellationToken ct)
    {
        EnsureWsConnected();
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Room code is empty.");

        ResetSession();

        var session = new CallSession();
        session.SetRoomCode(code.Trim());
        session.TransitionTo(CallState.Idle);
        SetCurrent(session);

        try
        {
            var localPort = SelectLocalUdpPort();
            var localLanEp = GetLocalLanEndpoint(localPort);

#if DEBUG
            var publicEp = localLanEp;
            Core.Logger.Info($"STUN skipped (DEBUG): using LAN endpoint {publicEp}");
#else
            var publicEp = await _stun.GetPublicEndPointAsync(localPort, timeoutMs: 5000, ct).ConfigureAwait(false);
#endif

            session.SetLocal(localPort, publicEp, localLanEp);

            StartUdpIfNeeded(session);

            var ipEndPoint = publicEp?.ToString() ?? localLanEp?.ToString() ?? new IPEndPoint(IPAddress.Loopback, localPort).ToString();
            Core.Logger.Info($"Calls.Join: code={code} localPort={localPort} ipEndPoint='{ipEndPoint}'");

            var msg = new ConnectToSession
            {
                Code = code.Trim(),
                Value = code.Trim(),
                IpEndPoint = ipEndPoint
            };

            var ctx = Context.Create(msg);
            var json = JsonSerializer.Serialize(ctx);

            // Wait for either ConnectedToSession or first InterlocutorJoined. Both are a signal that connect succeeded.
            await AwaitConnectSucceededAsync(() => _connection.SendAsync(json, ct), ct).ConfigureAwait(false);

            // Confirm handshake to backend.
            var successCtx = Context.Create(new SuccessConnectedSession { Value = string.Empty });
            await _connection.SendAsync(JsonSerializer.Serialize(successCtx), ct).ConfigureAwait(false);

            Transition(session, CallState.Connected);
            return session;
        }
        catch
        {
            ResetSession();
            throw;
        }
    }

    private async Task HangupInternalAsync(string reason, CancellationToken ct)
    {
        CallSession? session;
        lock (_sync)
        {
            session = Current;
        }

        if (session is null)
            return;

        try
        {
            var hangup = Context.Create(new HangupSession { Value = reason });
            await _connection.SendAsync(JsonSerializer.Serialize(hangup), ct).ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            _controls.SendHangup();
        }
        catch
        {
        }

        ResetSession();
    }

    private void OnWsMessage(object? sender, string raw)
    {
        CallSession? session;
        lock (_sync)
        {
            session = Current;
        }

        if (session is null)
            return;

        Context? ctx;
        try
        {
            ctx = JsonSerializer.Deserialize<Context>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return;
        }

        if (ctx is null)
            return;

        try
        {
            var msg = ctx.ToMessage();
            switch (msg)
            {
                case InterlocutorJoined joined:
                    HandleInterlocutorJoined(session, joined);
                    break;
                case InterlocutorLeft left:
                    HandleInterlocutorLeft(session, left);
                    break;
            }
        }
        catch
        {
        }
    }

    private void HandleInterlocutorJoined(CallSession session, InterlocutorJoined joined)
    {
        if (string.IsNullOrWhiteSpace(joined.Id))
            return;

        if (!TryParseIpEndPoint(joined.IpEndPoint, out var remote))
        {
            Core.Logger.Warn($"InterlocutorJoined with invalid IpEndPoint: id={joined.Id} ip='{joined.IpEndPoint}'");
            return;
        }

        lock (_sync)
        {
            if (session.Interlocutors.All(x => x.Id != joined.Id))
                session.Interlocutors.Add(new Interlocutor(joined.Id, remote, CallState.Connected));
        }

        try
        {
            _udp.AddInterlocutor(joined.Id, remote);
        }
        catch (Exception ex)
        {
            Core.Logger.Error("UDP AddInterlocutor failed", ex);
        }

        try
        {
            _controls.SendMuteStateToInterlocutor(_audio.IsMicrophoneEnabled, joined.Id);
        }
        catch { }

        try
        {
            var name = GetLocalDisplayName();
            if (!string.IsNullOrEmpty(name))
                _controls.SendDisplayName(name, joined.Id);
        }
        catch { }

        try
        {
            var hash = _avatarProvider?.GetOwnAvatarHash();
            if (hash != null)
                _avatarExchange.SendHash(hash, joined.Id);
            else
                _avatarExchange.SendEmptyHash(joined.Id);
        }
        catch { }

        Core.Logger.Info($"InterlocutorJoined: {joined.Id} {remote}");

        if (session.State != CallState.Connected)
            Transition(session, CallState.Connected);
    }

    private void HandleInterlocutorLeft(CallSession session, InterlocutorLeft left)
    {
        if (string.IsNullOrWhiteSpace(left.InterlocutorId))
            return;

        lock (_sync)
        {
            for (var i = session.Interlocutors.Count - 1; i >= 0; i--)
            {
                if (session.Interlocutors[i].Id == left.InterlocutorId)
                    session.Interlocutors.RemoveAt(i);
            }
        }

        try { _udp.RemoveInterlocutor(left.InterlocutorId); } catch { }
        try { _audio.RemoveInterlocutor(left.InterlocutorId); } catch { }

        Core.Logger.Info($"InterlocutorLeft: {left.InterlocutorId}");
    }

    private void Transition(CallSession session, CallState next)
    {
        session.TransitionTo(next);

        if (next == CallState.Connected)
        {
            try
            {
                if (!_audio.IsInitialized)
                    _audio.Initialize();
                _audio.StartCapture();
            }
            catch (Exception ex)
            {
                Core.Logger.Error("Failed to start audio", ex);
            }
        }

        StateChanged?.Invoke(this, next);
        Core.Logger.Info($"Call state: {next}");
    }

    private void SetCurrent(CallSession? session)
    {
        lock (_sync)
        {
            Current = session;
        }

        CurrentChanged?.Invoke(this, session);
    }

    private void EnsureWsConnected()
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("WebSocket is not connected.");
    }

    private void ResetSession()
    {
        lock (_sync)
        {
            try { _sessionCts?.Cancel(); } catch { }
            try { _sessionCts?.Dispose(); } catch { }
            _sessionCts = null;

            try { _audio.StopCapture(); } catch { }
            try { _udp.Stop(); } catch { }

            try { _controls.Detach(); } catch { }
            try { _avatarExchange.Detach(); } catch { }
            try { _chat.Detach(); } catch { }

            try { _udpClient?.Dispose(); } catch { }
            _udpClient = null;

            Current = null;
            _sentAudioCount = 0;
            _recvAudioCount = 0;
        }

        CurrentChanged?.Invoke(this, null);
    }

    private void EnsureSessionLifetime()
    {
        if (_sessionCts is not null)
            return;

        _sessionCts = new CancellationTokenSource();
    }

    private void StartUdpIfNeeded(CallSession session)
    {
        lock (_sync)
        {
            EnsureSessionLifetime();

            _udpClient = new UdpClient(session.LocalUdpPort);
            _udp.Start(_udpClient, _sessionCts!.Token);

            _controls.Attach(_udp);
            _avatarExchange.Attach(_udp);
            _chat.Attach(_udp);
        }
    }

    private void OnRemoteHangupByInterlocutor(string interlocutorId)
    {
        try
        {
            var session = Current;
            if (session is null)
                return;

            // In mesh, a peer's UDP Hangup means that peer is leaving, not that the entire call must end.
            HandleInterlocutorLeft(session, new InterlocutorLeft
            {
                Value = "RemoteHangup",
                InterlocutorId = interlocutorId
            });
        }
        catch
        {
        }
    }

    private void OnRemoteAvatarHash(string interlocutorId, byte[] remoteHash)
    {
        try
        {
            if (_avatarProvider == null) return;

            var allZero = true;
            foreach (var b in remoteHash) { if (b != 0) { allZero = false; break; } }
            if (allZero) return;

            var cached = _avatarProvider.GetCachedHash(interlocutorId);
            if (cached != null && cached.AsSpan().SequenceEqual(remoteHash)) return;

            _avatarExchange.SendRequest(interlocutorId);
        }
        catch { }
    }

    private void OnRemoteAvatarRequested(string interlocutorId)
    {
        try
        {
            var bytes = _avatarProvider?.GetOwnAvatarBytes();
            if (bytes is { Length: > 0 })
                _avatarExchange.SendAvatar(bytes, interlocutorId);
        }
        catch { }
    }

    private void OnRemoteAvatarReceived(string interlocutorId, byte[] data)
    {
        try
        {
            var hash = UdpAvatarExchange.ComputeHash(data);
            _avatarProvider?.SaveRemoteAvatar(interlocutorId, data, hash);
            AvatarReceived?.Invoke(interlocutorId, data);
        }
        catch { }
    }

    private void OnRemoteMuteChangedByInterlocutor(string interlocutorId, bool isMuted)
    {
        try
        {
            var session = Current;
            if (session is null)
                return;

            lock (_sync)
            {
                var it = session.Interlocutors.FirstOrDefault(x => x.Id == interlocutorId);
                if (it is not null)
                    it.IsMuted = isMuted;
            }

            Core.Logger.Info($"Remote mute changed: {interlocutorId} muted={isMuted}");
            MuteStateChanged?.Invoke(interlocutorId, isMuted);
        }
        catch
        {
        }
    }

    public void SendChatMessage(string text)
    {
        _chat.SendMessage(GetLocalDisplayName(), text);
    }

    private string GetLocalDisplayName()
    {
        return _userSettings?.DisplayName
            ?? _auth?.Username
            ?? "";
    }

    private void OnRemoteDisplayNameReceived(string interlocutorId, string name)
    {
        try
        {
            var session = Current;
            if (session is null) return;

            lock (_sync)
            {
                var it = session.Interlocutors.FirstOrDefault(x => x.Id == interlocutorId);
                if (it is not null)
                    it.DisplayName = name;
            }

            Core.Logger.Info($"DisplayName received: {interlocutorId} = {name}");
            DisplayNameReceived?.Invoke(interlocutorId, name);
        }
        catch { }
    }

    private async Task<T> AwaitMessageAsync<T>(Func<Task> send, Func<T, bool> predicate, CancellationToken ct) where T : class
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? s, string raw)
        {
            try
            {
                var ctx = JsonSerializer.Deserialize<Context>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (ctx is null)
                    return;

                var msg = ctx.ToMessage();
                if (msg is T typed && predicate(typed))
                    tcs.TrySetResult(typed);
            }
            catch
            {
            }
        }

        _connection.MessageReceived += Handler;
        try
        {
            await send().ConfigureAwait(false);
            using var _ = ct.Register(() => tcs.TrySetCanceled(ct));
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _connection.MessageReceived -= Handler;
        }
    }

    private async Task AwaitConnectSucceededAsync(Func<Task> send, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? s, string raw)
        {
            try
            {
                var ctx = JsonSerializer.Deserialize<Context>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (ctx is null)
                    return;

                if (string.Equals(ctx.Type, "Messages.NoAuthCall.ConnectedToSession", StringComparison.Ordinal) ||
                    string.Equals(ctx.Type, "Messages.NoAuthCall.InterlocutorJoined", StringComparison.Ordinal))
                {
                    tcs.TrySetResult();
                }

                if (string.Equals(ctx.Type, "Messages.NoAuthCall.ErrorConnectToSession", StringComparison.Ordinal))
                {
                    if (ctx.Message.ValueKind == JsonValueKind.Object &&
                        ctx.Message.TryGetProperty("Value", out var valueEl) &&
                        valueEl.ValueKind == JsonValueKind.String)
                    {
                        tcs.TrySetException(new InvalidOperationException(valueEl.GetString() ?? "ErrorConnectToSession"));
                    }
                    else
                    {
                        tcs.TrySetException(new InvalidOperationException("ErrorConnectToSession"));
                    }
                }
            }
            catch
            {
            }
        }

        _connection.MessageReceived += Handler;
        try
        {
            await send().ConfigureAwait(false);
            using var _ = ct.Register(() => tcs.TrySetCanceled(ct));
            await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _connection.MessageReceived -= Handler;
        }
    }

    private static int SelectLocalUdpPort()
    {
#if DEBUG
        return 5000 + Random.Shared.Next(1000);
#else
        return 40000 + Random.Shared.Next(20000);
#endif
    }

    private static bool TryParseIpEndPoint(string s, out IPEndPoint ep)
    {
        ep = new IPEndPoint(IPAddress.Any, 0);
        try
        {
            ep = IPEndPoint.Parse(s);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IPEndPoint? GetLocalLanEndpoint(int port)
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in interfaces)
            {
                var ipProps = ni.GetIPProperties();
                var addr = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (addr?.Address is not null)
                    return new IPEndPoint(addr.Address, port);
            }
        }
        catch
        {
        }

        return null;
    }
}