using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Core.Domain.Calls;
using Core.Public.Services;
using Core.Websockets;
using Core.Websockets.Messages.NoAuthCall;
using Microsoft.Extensions.DependencyInjection;

namespace NewAppMaui;

public partial class JoinRoomPage : ContentPage
{
    private readonly IConnectionService _connection;
    private readonly ICallsService _calls;
    private CancellationTokenSource? _cts;

    public JoinRoomPage()
    {
        _connection = ((App)Application.Current!).Services.GetRequiredService<IConnectionService>();
        _calls = ((App)Application.Current!).Services.GetRequiredService<ICallsService>();
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

            var session = await _calls.JoinAsync(code, ct).ConfigureAwait(false);

            var initial = session.Interlocutors
                .Select(x => new InterlocutorJoined { Id = x.Id, IpEndPoint = x.RemoteIp.ToString() })
                .ToArray();

            var page = ((App)Application.Current!).Services.GetRequiredService<CallRoomPage>();
            page.InitializeRoom(session.RoomCode, initial);
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

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
