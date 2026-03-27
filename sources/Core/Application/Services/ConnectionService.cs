using System.Data;
using Core.Public.Configurations;
using Core.Public.Services;

namespace Core.Application.Services;

public class ConnectionService : IConnectionService
{
    private readonly IConnectionSettingsStore _settingsStore;

    public ConnectionService(IConnectionSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public ConnectionState State { get; private set; } = ConnectionState.Closed;
    public event EventHandler<ConnectionState>? StateChanged;
    public Task ConnectAsync(CancellationToken ct = default)
    {
        return ConnectInternalAsync(ct);
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        if (State != ConnectionState.Closed)
        {
            State = ConnectionState.Closed;
            StateChanged?.Invoke(this, State);
        }

        return Task.CompletedTask;
    }

    public Task<bool> PingAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    private async Task ConnectInternalAsync(CancellationToken ct)
    {
        var profile = await _settingsStore.GetActiveAsync(ct).ConfigureAwait(false);
        if (profile is null)
            throw new InvalidOperationException("No active connection profile.");

        _ = BuildWebSocketUri(profile);

        State = ConnectionState.Open;
        StateChanged?.Invoke(this, State);
    }

    private static Uri BuildWebSocketUri(ConnectionProfile profile)
    {
        var scheme = profile.UseSsl ? "wss" : "ws";
        return new Uri($"{scheme}://{profile.Domain}:{profile.Port}/connector");
    }
}