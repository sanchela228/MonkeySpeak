using System.Linq;
using Core.Public.Services;
using Core.Websockets.Messages.NoAuthCall;
using Microsoft.Extensions.DependencyInjection;

namespace NewAppMaui;

public partial class IncomingFriendCallPage : ContentPage
{
    private readonly IFriendCallsService _friendCalls;
    private readonly ICallsService _calls;

    private CancellationTokenSource? _cts;

    private string _roomCode = string.Empty;
    private string _fromUserId = string.Empty;
    private string _fromUsername = string.Empty;

    private bool _handled;

    public string RoomCode => _roomCode;

    public IncomingFriendCallPage()
    {
        var services = ((App)Application.Current!).Services;
        _friendCalls = services.GetRequiredService<IFriendCallsService>();
        _calls = services.GetRequiredService<ICallsService>();
        InitializeComponent();
    }

    public void Initialize(IncomingCallInvite invite)
    {
        _roomCode = invite.RoomCode ?? string.Empty;
        _fromUserId = invite.FromUserId ?? string.Empty;
        _fromUsername = invite.FromUsername ?? string.Empty;

        CallerLabel.Text = string.IsNullOrWhiteSpace(_fromUsername) ? "Incoming call" : $"Incoming call from {_fromUsername}";
        StatusLabel.Text = "";
        ErrorLabel.IsVisible = false;
        ErrorLabel.Text = string.Empty;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _cts = new CancellationTokenSource();

        var ct = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            if (_handled)
                return;

            _handled = true;

            try
            {
                await _friendCalls.RespondAsync(_fromUserId, _roomCode, accepted: false, reason: "No answer", ct);
            }
            catch
            {
            }

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
            });
        }, ct);
    }

    protected override void OnDisappearing()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnDisappearing();
    }

    public void MarkCancelledByCaller()
    {
        if (_handled)
            return;

        _handled = true;

        try
        {
            _cts?.Cancel();
        }
        catch
        {
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                AcceptBtn.IsEnabled = false;
                RejectBtn.IsEnabled = false;
                Loading.IsVisible = false;
                Loading.IsRunning = false;
                StatusLabel.Text = "Caller cancelled";
            }
            catch
            {
            }
        });

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await DisplayAlert("Call", "Caller cancelled", "OK");
            }
            catch
            {
            }

            try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
        });
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        if (_handled)
            return;

        _handled = true;
        var ct = _cts?.Token ?? CancellationToken.None;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            AcceptBtn.IsEnabled = false;
            RejectBtn.IsEnabled = false;
            Loading.IsVisible = true;
            Loading.IsRunning = true;
            StatusLabel.Text = "Connecting...";
            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = string.Empty;
        });

        try
        {
            if (_calls.Current is not null)
            {
                await _friendCalls.RespondAsync(_fromUserId, _roomCode, accepted: false, reason: "Busy", ct);
                await Shell.Current.Navigation.PopModalAsync();
                return;
            }

            await _friendCalls.RespondAsync(_fromUserId, _roomCode, accepted: true, reason: string.Empty, ct);

            if (ct.IsCancellationRequested)
                throw new OperationCanceledException(ct);

            var session = await _calls.JoinAsync(_roomCode, ct).ConfigureAwait(false);

            var initial = session.Interlocutors
                .Select(x => new InterlocutorJoined { Id = x.Id, IpEndPoint = x.RemoteIp.ToString() })
                .ToArray();

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try { await Shell.Current.Navigation.PopModalAsync(); } catch { }

                var callPage = ((App)Application.Current!).Services.GetRequiredService<CallRoomPage>();
                callPage.InitializeRoom(session.RoomCode, initial);
                await Shell.Current.Navigation.PushModalAsync(new NavigationPage(callPage));
            });
        }
        catch (OperationCanceledException)
        {
            try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Loading.IsRunning = false;
                Loading.IsVisible = false;
                ErrorLabel.Text = ex.Message;
                ErrorLabel.IsVisible = true;
                StatusLabel.Text = "Failed";
            });
        }
    }

    private async void OnRejectClicked(object? sender, EventArgs e)
    {
        if (_handled)
            return;

        _handled = true;

        var ct = _cts?.Token ?? CancellationToken.None;

        try
        {
            await _friendCalls.RespondAsync(_fromUserId, _roomCode, accepted: false, reason: "Rejected", ct);
        }
        catch
        {
        }

        try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
    }
}
