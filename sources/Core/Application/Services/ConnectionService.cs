using System.Data;
using Core.Public.Configurations;
using Core.Public.Services;
using Core.Application.Abstractions;

namespace Core.Application.Services;

public class ConnectionService : IConnectionService
{
    private readonly IConnectionSettingsStore _settingsStore;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly SemaphoreSlim _reconnectSync = new(1, 1);
    private readonly IWebSocketClient _ws;
    private readonly object _connectCtsSync = new();
    private CancellationTokenSource _connectCts = new();
    private string? _lastActiveProfileId;

    public ConnectionService(IConnectionSettingsStore settingsStore, IWebSocketClient ws)
    {
        _settingsStore = settingsStore;
        _ws = ws;
        _settingsStore.Changed += OnSettingsChanged;
        _ = InitializeAsync();

        _ws.MessageReceived += (_, text) => MessageReceived?.Invoke(this, text);

        Core.Logger.Info("ConnectionService created");
    }

    public Exception? LastError { get; private set; }
    public ConnectionState State { get; private set; } = ConnectionState.Closed;
    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<string>? MessageReceived;
    public Task ConnectAsync(CancellationToken ct = default)
    {
        return ConnectInternalAsync(ct);
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        return DisconnectInternalAsync(ct);
    }

    public Task SendAsync(string text, CancellationToken ct = default)
    {
        return _ws.SendAsync(text, ct);
    }

    public Task<bool> PingAsync(CancellationToken ct = default)
    {
        return Task.FromResult(State == ConnectionState.Open);
    }

    private async Task ConnectInternalAsync(CancellationToken ct)
    {
        await _sync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (State == ConnectionState.Open || State == ConnectionState.Connecting)
                return;

            using var linkedCts = CreateLinkedConnectCts(ct);
            var connectToken = linkedCts.Token;

            var profile = await _settingsStore.GetActiveAsync(ct).ConfigureAwait(false);
            if (profile is null)
                throw new InvalidOperationException("No active connection profile.");

            var uri = BuildWebSocketUri(profile);
            var maxRetries = Math.Max(0, profile.MaxRetries);
            Exception? lastError = null;

            LastError = null;

            Core.Logger.Info($"WS connect requested: profile={profile.Id} domain={profile.Domain}:{profile.Port} ssl={profile.UseSsl} retries={maxRetries} uri={uri}");

            try
            {
                for (var attempt = 0; attempt <= maxRetries; attempt++)
                {
                    SetState(ConnectionState.Connecting);

                    Core.Logger.Debug($"WS connect attempt {attempt + 1}/{maxRetries + 1}: {uri}");

                    try
                    {
                        await _ws.ConnectAsync(uri, connectToken).ConfigureAwait(false);

                        SetState(ConnectionState.Open);

                        Core.Logger.Info($"WS connected: {uri}");
                        return;
                    }
                    catch (Exception ex) when (!connectToken.IsCancellationRequested)
                    {
                        lastError = ex;
                        LastError = ex;

                        Core.Logger.Warn($"WS connect failed (attempt {attempt + 1}/{maxRetries + 1}): {ex.GetType().Name}: {ex.Message}");

                        if (attempt >= maxRetries)
                            break;

                        SetState(ConnectionState.Broken);
                        await Task.Delay(TimeSpan.FromSeconds(2), connectToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Core.Logger.Info("WS connect canceled (likely profile changed / disconnect)");
                SetState(ConnectionState.Closed);
                return;
            }

            SetState(ConnectionState.Broken);
            throw new InvalidOperationException($"Failed to connect to {uri}.", lastError);
        }
        finally
        {
            _sync.Release();
        }
    }

    private static Uri BuildWebSocketUri(ConnectionProfile profile)
    {
        var scheme = profile.UseSsl ? "wss" : "ws";

        var domain = profile.Domain;
        if (OperatingSystem.IsAndroid() &&
            (string.Equals(domain, "localhost", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(domain, "127.0.0.1", StringComparison.OrdinalIgnoreCase)))
        {
            Core.Logger.Info("Android localhost detected, rewriting to 10.0.2.2");
            domain = "10.0.2.2";
        }

        return new Uri($"{scheme}://{domain}:{profile.Port}/connector");
    }

    private async Task DisconnectInternalAsync(CancellationToken ct)
    {
        CancelOngoingConnect();
        await _sync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Core.Logger.Info("WS disconnect requested");
            await _ws.DisconnectAsync(ct).ConfigureAwait(false);
            SetState(ConnectionState.Closed);
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            var profile = await _settingsStore.GetActiveAsync().ConfigureAwait(false);
            _lastActiveProfileId = profile?.Id;

            Core.Logger.Info($"ConnectionService initialized. ActiveProfileId={_lastActiveProfileId ?? "<null>"}");

            if (!_settingsStore.HasExplicitActiveProfileSelection)
                return;

            await ConnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LastError = ex;
            Core.Logger.Error("ConnectionService initialization failed", ex);
            SetState(ConnectionState.Broken);
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        Core.Logger.Info("Connection settings changed");
        CancelOngoingConnect();
        _ = Task.Run(ReconnectIfProfileChangedAsync);
    }

    private async Task ReconnectIfProfileChangedAsync()
    {
        await _reconnectSync.WaitAsync().ConfigureAwait(false);
        try
        {
            ConnectionProfile? active;

            try
            {
                active = await _settingsStore.GetActiveAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LastError = ex;
                SetState(ConnectionState.Broken);
                return;
            }

            var activeId = active?.Id;
            if (string.Equals(activeId, _lastActiveProfileId, StringComparison.Ordinal))
                return;

            Core.Logger.Info($"Active profile changed: {_lastActiveProfileId ?? "<null>"} -> {activeId ?? "<null>"}");

            _lastActiveProfileId = activeId;

            try
            {
                CancelOngoingConnect();
                await DisconnectAsync().ConfigureAwait(false);
                await ConnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LastError = ex;
                Core.Logger.Error("Reconnect after profile change failed", ex);
                SetState(ConnectionState.Broken);
            }
        }
        finally
        {
            _reconnectSync.Release();
        }
    }

    private CancellationTokenSource CreateLinkedConnectCts(CancellationToken external)
    {
        lock (_connectCtsSync)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(external, _connectCts.Token);
        }
    }

    private void CancelOngoingConnect()
    {
        lock (_connectCtsSync)
        {
            try
            {
                _connectCts.Cancel();
            }
            catch
            {
            }

            Core.Logger.Debug("Ongoing WS connect cancelled");

            _connectCts.Dispose();
            _connectCts = new CancellationTokenSource();
        }
    }

    private void SetState(ConnectionState next)
    {
        if (State == next)
            return;

        Core.Logger.Debug($"Connection state: {State} -> {next}");
        State = next;
        StateChanged?.Invoke(this, next);
    }
}