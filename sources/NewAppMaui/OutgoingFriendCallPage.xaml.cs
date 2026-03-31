using System.Linq;
using Core.Public.Services;
using Core.Websockets.Messages.NoAuthCall;
using Microsoft.Extensions.DependencyInjection;

namespace NewAppMaui;

public partial class OutgoingFriendCallPage : ContentPage
{
    private readonly ICallsService _calls;
    private readonly IFriendCallsService _friendCalls;

    private CancellationTokenSource? _cts;

    private string _friendId = string.Empty;
    private string _friendUsername = string.Empty;
    private string _roomCode = string.Empty;

    private bool _finished;
    private bool _answered;

    public OutgoingFriendCallPage()
    {
        var services = ((App)Application.Current!).Services;
        _calls = services.GetRequiredService<ICallsService>();
        _friendCalls = services.GetRequiredService<IFriendCallsService>();
        InitializeComponent();
    }

    public void Initialize(string friendId, string friendUsername)
    {
        _friendId = friendId ?? string.Empty;
        _friendUsername = friendUsername ?? string.Empty;
        TitleLabel.Text = string.IsNullOrWhiteSpace(_friendUsername) ? "Calling" : $"Calling {_friendUsername}";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _cts = new CancellationTokenSource();

        _calls.StateChanged += OnCallStateChanged;
        _friendCalls.InviteResponse += OnInviteResponse;

        _ = StartAsync(_cts.Token);
    }

    protected override void OnDisappearing()
    {
        _calls.StateChanged -= OnCallStateChanged;
        _friendCalls.InviteResponse -= OnInviteResponse;

        if (!_finished && !_answered && !string.IsNullOrWhiteSpace(_roomCode) && !string.IsNullOrWhiteSpace(_friendId))
        {
            try
            {
                _ = _friendCalls.CancelAsync(_friendId, _roomCode, CancellationToken.None);
            }
            catch
            {
            }

            try
            {
                _ = _calls.HangupAsync(CancellationToken.None);
            }
            catch
            {
            }
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        base.OnDisappearing();
    }

    private async Task StartAsync(CancellationToken ct)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = string.Empty;
            Loading.IsVisible = true;
            Loading.IsRunning = true;
            StatusLabel.Text = "Creating room...";
            RoomCodeLabel.Text = string.Empty;
        });

        try
        {
            var session = await _calls.CreateAsync(ct).ConfigureAwait(false);
            _roomCode = session.RoomCode;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = "Sending invite...";
                RoomCodeLabel.Text = _roomCode.ToUpperInvariant();
            });

            await _friendCalls.SendInviteAsync(_friendId, _roomCode, ct).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = "Ringing...";
            });

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

                if (_finished || _answered)
                    return;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await EndWithMessageAsync("No answer", cancelInvite: true);
                });
            }, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await EndWithMessageAsync(ex.Message, cancelInvite: false);
            });
        }
    }

    private void OnInviteResponse(object? sender, CallInviteResponseInfo e)
    {
        if (string.IsNullOrWhiteSpace(_roomCode) || !string.Equals(_roomCode, e.RoomCode, StringComparison.OrdinalIgnoreCase))
            return;

        _answered = true;

        if (e.Accepted)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = "Accepted. Connecting...";
            });
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var reason = string.IsNullOrWhiteSpace(e.Reason) ? "Rejected" : e.Reason;
                await EndWithMessageAsync(reason, cancelInvite: false);
            });
        }
    }

    private void OnCallStateChanged(object? sender, Core.Domain.Calls.CallState e)
    {
        if (e != Core.Domain.Calls.CallState.Connected)
            return;

        if (_finished)
            return;

        _finished = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var session = _calls.Current;
                if (session is null)
                    return;

                var initial = session.Interlocutors
                    .Select(x => new InterlocutorJoined { Id = x.Id, IpEndPoint = x.RemoteIp.ToString() })
                    .ToArray();

                var callPage = ((App)Application.Current!).Services.GetRequiredService<CallRoomPage>();
                callPage.InitializeRoom(session.RoomCode, initial);

                await Shell.Current.Navigation.PopModalAsync();
                await Shell.Current.Navigation.PushModalAsync(new NavigationPage(callPage));
            }
            catch
            {
            }
        });
    }

    private async Task EndWithMessageAsync(string message, bool cancelInvite)
    {
        if (_finished)
            return;

        _finished = true;

        try
        {
            if (cancelInvite && !string.IsNullOrWhiteSpace(_roomCode) && !string.IsNullOrWhiteSpace(_friendId))
            {
                try { await _friendCalls.CancelAsync(_friendId, _roomCode); } catch { }
            }

            try { await _calls.HangupAsync(); } catch { }
        }
        finally
        {
            Loading.IsRunning = false;
            Loading.IsVisible = false;
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
            StatusLabel.Text = "Ended";
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_roomCode) && !string.IsNullOrWhiteSpace(_friendId))
            {
                try { await _friendCalls.CancelAsync(_friendId, _roomCode, CancellationToken.None); } catch { }
            }

            try { await _calls.HangupAsync(CancellationToken.None); } catch { }

            _cts?.Cancel();
        }
        catch
        {
        }

        try { await Shell.Current.Navigation.PopModalAsync(); } catch { }
    }
}
