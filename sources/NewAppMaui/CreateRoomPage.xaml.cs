using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Core.Public.Services;
using Core.Websockets;
using Core.Websockets.Messages.NoAuthCall;
using Microsoft.Extensions.DependencyInjection;

namespace NewAppMaui;

public partial class CreateRoomPage : ContentPage
{
    private readonly IConnectionService _connection;
    private CancellationTokenSource? _cts;
    private string _roomCode = string.Empty;

    public CreateRoomPage()
    {
        _connection = ((App)Application.Current!).Services.GetRequiredService<IConnectionService>();
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _cts = new CancellationTokenSource();
        _ = StartAsync(_cts.Token);
    }

    protected override void OnDisappearing()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnDisappearing();
    }

    private async Task StartAsync(CancellationToken ct)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Loading.IsVisible = true;
            Loading.IsRunning = true;
            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = string.Empty;
            CodeLabel.Text = string.Empty;
        });

        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await ShowErrorAsync("WebSocket is not connected.");
            return;
        }

        try
        {
            var ipEndPoint = BuildLocalIpEndPointString(SelectLocalUdpPort());

            var msg = new CreateSession
            {
                Value = string.Empty,
                IpEndPoint = ipEndPoint
            };

            var request = Context.Create(msg);
            var json = System.Text.Json.JsonSerializer.Serialize(request);

            var code = await AwaitSingleMessageAsync(
                send: () => _connection.SendAsync(json, ct),
                expectedType: "Messages.NoAuthCall.SessionCreated",
                ct: ct);

            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Empty room code returned.");

            try { await Clipboard.Default.SetTextAsync(code); } catch { }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CodeLabel.Text = code.ToUpperInvariant();
                CodeLabel.IsVisible = true;
                Loading.IsRunning = false;
                Loading.IsVisible = false;
            });

            _roomCode = code;

            _ = Task.Run(() => WaitForJoinAndNavigateAsync(ct));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async Task WaitForJoinAndNavigateAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<InterlocutorJoined?>(TaskCreationOptions.RunContinuationsAsynchronously);

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

                if (!string.Equals(ctx.Type, "Messages.NoAuthCall.InterlocutorJoined", StringComparison.Ordinal))
                    return;

                try
                {
                    if (ctx.ToMessage() is InterlocutorJoined joined)
                    {
                        tcs.TrySetResult(joined);
                        return;
                    }
                }
                catch
                {
                }

                tcs.TrySetResult(null);
            }
            catch
            {
            }
        }

        _connection.MessageReceived += Handler;
        try
        {
            using var _ = ct.Register(() => tcs.TrySetCanceled(ct));
            var joined = await tcs.Task.ConfigureAwait(false);

            var page = ((App)Application.Current!).Services.GetRequiredService<CallRoomPage>();
            page.InitializeRoom(_roomCode, joined is null ? null : new[] { joined });
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.Navigation.PushModalAsync(new NavigationPage(page));
            });
        }
        catch
        {
        }
        finally
        {
            _connection.MessageReceived -= Handler;
        }
    }

    private async Task<string?> AwaitSingleMessageAsync(Func<Task> send, string expectedType, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

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

                if (!string.Equals(ctx.Type, expectedType, StringComparison.Ordinal))
                    return;

                if (ctx.Message.ValueKind != JsonValueKind.Object)
                    return;

                if (ctx.Message.TryGetProperty("Value", out var valueEl) && valueEl.ValueKind == JsonValueKind.String)
                {
                    tcs.TrySetResult(valueEl.GetString());
                    return;
                }

                tcs.TrySetResult(null);
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

    private async Task ShowErrorAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Loading.IsRunning = false;
            Loading.IsVisible = false;
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
        });
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
