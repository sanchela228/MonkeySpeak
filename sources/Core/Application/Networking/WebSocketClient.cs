using System.Net.WebSockets;
using System.Text;

using Core.Application.Abstractions;

namespace Core.Application.Networking;

public sealed class WebSocketClient : IWebSocketClient
{
    private readonly SemaphoreSlim _sendSync = new(1, 1);
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoopTask;

    public event EventHandler<string>? MessageReceived;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler? Reconnecting;

    public async Task ConnectAsync(Uri uri, CancellationToken ct)
    {
        Logger.Info($"WebSocketClient.ConnectAsync: {uri}");
        await DisconnectAsync(ct).ConfigureAwait(false);

        _ws = new ClientWebSocket();
        _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        await _ws.ConnectAsync(uri, _cts.Token).ConfigureAwait(false);

        if (_ws.State == WebSocketState.Open)
        {
            Connected?.Invoke(this, EventArgs.Empty);
            Logger.Info("WebSocketClient connected");
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_ws, _cts.Token));
        }
        else
        {
            Logger.Warn($"WebSocketClient connect finished with state={_ws.State}");
            throw new WebSocketException($"WebSocket state is '{_ws.State}' after ConnectAsync.");
        }
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        Logger.Debug("WebSocketClient.DisconnectAsync");
        var ws = _ws;
        _ws = null;

        try
        {
            _cts?.Cancel();
        }
        catch
        {
        }

        if (ws is not null)
        {
            try
            {
                if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
            }
            finally
            {
                ws.Dispose();
            }
        }

        _cts?.Dispose();
        _cts = null;
        _receiveLoopTask = null;

        Disconnected?.Invoke(this, EventArgs.Empty);
        Logger.Info("WebSocketClient disconnected");
    }

    public async Task SendAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var ws = _ws;
        if (ws?.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not connected.");

        await _sendSync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            Logger.Debug($"WebSocketClient.SendAsync: {bytes.Length} bytes");
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, cancellationToken: ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _sendSync.Release();
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        Logger.Debug("WebSocketClient receive loop started");

        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                ms.SetLength(0);

                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                        return;

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        Logger.Debug($"WebSocketClient received: {text.Length} chars");
                        Logger.Debug($"WebSocketClient received: {text}");
                        MessageReceived?.Invoke(this, text);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Error("WebSocketClient receive loop error", ex);
        }
        finally
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
            Logger.Debug("WebSocketClient receive loop finished");
        }
    }
}
