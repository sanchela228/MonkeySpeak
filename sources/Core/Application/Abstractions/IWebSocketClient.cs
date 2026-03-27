namespace Core.Application.Abstractions;

public interface IWebSocketClient
{
    Task ConnectAsync(Uri uri, CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
    Task SendAsync(string text, CancellationToken ct);
    event EventHandler<string> MessageReceived;
    event EventHandler Connected;
    event EventHandler Disconnected;
    event EventHandler Reconnecting;
}