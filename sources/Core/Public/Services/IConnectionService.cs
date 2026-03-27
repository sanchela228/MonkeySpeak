using System;
using System.Data;

namespace Core.Public.Services;

public interface IConnectionService
{
    Exception? LastError { get; }

    ConnectionState State { get; }
    event EventHandler<ConnectionState>? StateChanged;

    event EventHandler<string>? MessageReceived;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    Task SendAsync(string text, CancellationToken ct = default);

    Task<bool> PingAsync(CancellationToken ct = default);
}