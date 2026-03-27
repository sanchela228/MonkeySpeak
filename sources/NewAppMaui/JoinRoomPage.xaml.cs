using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Core.Public.Services;
using Core.Websockets;
using Core.Websockets.Messages.NoAuthCall;
using Microsoft.Extensions.DependencyInjection;

namespace NewAppMaui;

public partial class JoinRoomPage : ContentPage
{
    private readonly IConnectionService _connection;
    private CancellationTokenSource? _cts;

    public JoinRoomPage()
    {
        _connection = ((App)Application.Current!).Services.GetRequiredService<IConnectionService>();
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _cts = new CancellationTokenSource();
    }

    protected override void OnDisappearing()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnDisappearing();
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        var ct = _cts?.Token ?? CancellationToken.None;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = string.Empty;
            Loading.IsVisible = true;
            Loading.IsRunning = true;
            ConnectButton.IsEnabled = false;
        });

        try
        {
            if (_connection.State != System.Data.ConnectionState.Open)
                throw new InvalidOperationException("WebSocket is not connected.");

            var code = (CodeEntry.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Please enter a code.");

            var ipEndPoint = BuildLocalIpEndPointString(SelectLocalUdpPort());

            var msg = new ConnectToSession
            {
                Code = code,
                Value = code,
                IpEndPoint = ipEndPoint
            };

            var request = Context.Create(msg);
            var json = System.Text.Json.JsonSerializer.Serialize(request);

            var result = await AwaitConnectResultAsync(() => _connection.SendAsync(json, ct), ct);

            if (!result.Success)
                throw new InvalidOperationException(result.ErrorMessage ?? "Failed to connect.");

            var successCtx = Context.Create(new SuccessConnectedSession { Value = string.Empty });
            await _connection.SendAsync(System.Text.Json.JsonSerializer.Serialize(successCtx), ct);

            var page = ((App)Application.Current!).Services.GetRequiredService<CallRoomPage>();
            page.InitializeRoom(code, result.InitialParticipants);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.Navigation.PushModalAsync(new NavigationPage(page));
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ErrorLabel.Text = ex.Message;
                ErrorLabel.IsVisible = true;
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Loading.IsRunning = false;
                Loading.IsVisible = false;
                ConnectButton.IsEnabled = true;
            });
        }
    }

    private async Task<(bool Success, string? ErrorMessage, List<InterlocutorJoined> InitialParticipants)> AwaitConnectResultAsync(
        Func<Task> send,
        CancellationToken ct)
    {
        var participants = new List<InterlocutorJoined>();
        var tcs = new TaskCompletionSource<(bool, string?, List<InterlocutorJoined>)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = 0;

        void Handler(object? sender, string raw)
        {
            try
            {
                var ctx = System.Text.Json.JsonSerializer.Deserialize<Context>(raw, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (ctx is null)
                    return;

                // Backend sends InterlocutorJoined after ConnectToSession.
                if (string.Equals(ctx.Type, "Messages.NoAuthCall.InterlocutorJoined", StringComparison.Ordinal))
                {
                    try
                    {
                        if (ctx.ToMessage() is InterlocutorJoined joined)
                            participants.Add(joined);
                    }
                    catch
                    {
                    }

                    // Give a tiny window to collect multiple InterlocutorJoined (if room already has several participants).
                    if (Interlocked.Exchange(ref completed, 1) == 0)
                    {
                        _ = Task.Run(async () =>
                        {
                            try { await Task.Delay(150, ct).ConfigureAwait(false); } catch { }
                            tcs.TrySetResult((true, null, participants));
                        }, CancellationToken.None);
                    }

                    return;
                }

                if (string.Equals(ctx.Type, "Messages.NoAuthCall.ConnectedToSession", StringComparison.Ordinal))
                {
                    tcs.TrySetResult((true, null, participants));
                    return;
                }

                if (string.Equals(ctx.Type, "Messages.NoAuthCall.ErrorConnectToSession", StringComparison.Ordinal))
                {
                    if (ctx.Message.ValueKind == JsonValueKind.Object &&
                        ctx.Message.TryGetProperty("Value", out var valueEl) &&
                        valueEl.ValueKind == JsonValueKind.String)
                    {
                        tcs.TrySetResult((false, valueEl.GetString(), participants));
                        return;
                    }

                    tcs.TrySetResult((false, "ErrorConnectToSession", participants));
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
            return await tcs.Task.ConfigureAwait(false);
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

    private static string BuildLocalIpEndPointString(int port)
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in interfaces)
            {
                var ipProps = ni.GetIPProperties();
                var addr = ipProps.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (addr?.Address is not null)
                    return new IPEndPoint(addr.Address, port).ToString();
            }
        }
        catch
        {
        }

        return new IPEndPoint(IPAddress.Loopback, port).ToString();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
