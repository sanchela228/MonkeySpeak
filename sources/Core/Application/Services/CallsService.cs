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
    private readonly IStunClient _stun;
    private readonly UdpUnifiedManager _udp;
    private readonly object _sync = new();

    private CancellationTokenSource? _sessionCts;
    private UdpClient? _udpClient;

    public CallSession? Current { get; private set; }
    public event EventHandler<CallSession?>? CurrentChanged;
    public event EventHandler<CallState>? StateChanged;

    public CallsService(IConnectionService connection, IStunClient stun, UdpUnifiedManager udp)
    {
        _connection = connection;
        _stun = stun;
        _udp = udp;

        _connection.MessageReceived += OnWsMessage;
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

    public void SetMicrophoneEnabled(bool enabled)
    {
        throw new NotImplementedException();
    }

    public void SetPlaybackEnabled(bool enabled)
    {
        throw new NotImplementedException();
    }

    public void SetMicrophoneVolume(int percent)
    {
        throw new NotImplementedException();
    }

    public void SetPlaybackVolume(int percent)
    {
        throw new NotImplementedException();
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
            var publicEp = await _stun.GetPublicEndPointAsync(localPort, timeoutMs: 5000, ct).ConfigureAwait(false);
            var localLanEp = GetLocalLanEndpoint(localPort);
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
            var publicEp = await _stun.GetPublicEndPointAsync(localPort, timeoutMs: 5000, ct).ConfigureAwait(false);
            var localLanEp = GetLocalLanEndpoint(localPort);
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

        try
        {
            _udp.RemoveInterlocutor(left.InterlocutorId);
        }
        catch
        {
        }

        Core.Logger.Info($"InterlocutorLeft: {left.InterlocutorId}");
    }

    private void Transition(CallSession session, CallState next)
    {
        session.TransitionTo(next);
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

            try { _udp.Stop(); } catch { }

            try { _udpClient?.Dispose(); } catch { }
            _udpClient = null;

            Current = null;
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
        }
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