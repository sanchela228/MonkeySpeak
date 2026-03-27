using Core.Domain.Calls;
using Core.Public.Services;
using Microsoft.Extensions.DependencyInjection;

namespace NewAppMaui;

public partial class CreateRoomPage : ContentPage
{
    private readonly IConnectionService _connection;
    private readonly ICallsService _calls;
    private CancellationTokenSource? _cts;
    private string _roomCode = string.Empty;
    private bool _navigated;

    public CreateRoomPage()
    {
        _connection = ((App)Application.Current!).Services.GetRequiredService<IConnectionService>();
        _calls = ((App)Application.Current!).Services.GetRequiredService<ICallsService>();
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _navigated = false;
        _calls.StateChanged += OnCallStateChanged;
        _cts = new CancellationTokenSource();
        _ = StartAsync(_cts.Token);
    }

    protected override void OnDisappearing()
    {
        _calls.StateChanged -= OnCallStateChanged;
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
            var session = await _calls.CreateAsync(ct).ConfigureAwait(false);
            var code = session.RoomCode;

            try { await Clipboard.Default.SetTextAsync(code); } catch { }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CodeLabel.Text = code.ToUpperInvariant();
                CodeLabel.IsVisible = true;
                Loading.IsRunning = false;
                Loading.IsVisible = false;
            });

            _roomCode = code;

            // Navigation will happen in OnCallStateChanged when someone joins.
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
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

    private void OnCallStateChanged(object? sender, CallState e)
    {
        if (e != CallState.Connected)
            return;

        if (_navigated)
            return;

        _navigated = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var session = _calls.Current;
                if (session is null)
                    return;

                var initial = session.Interlocutors
                    .Select(x => new Core.Websockets.Messages.NoAuthCall.InterlocutorJoined
                    {
                        Id = x.Id,
                        IpEndPoint = x.RemoteIp.ToString()
                    })
                    .ToArray();

                var page = ((App)Application.Current!).Services.GetRequiredService<CallRoomPage>();
                page.InitializeRoom(session.RoomCode, initial);
                await Shell.Current.Navigation.PushModalAsync(new NavigationPage(page));
            }
            catch
            {
            }
        });
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
