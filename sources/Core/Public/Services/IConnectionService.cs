using System.Data;

namespace Core.Public.Services;

public interface IConnectionService
{
    ConnectionState State { get; }
    event EventHandler<ConnectionState>? StateChanged;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    Task<bool> PingAsync(CancellationToken ct = default);
}