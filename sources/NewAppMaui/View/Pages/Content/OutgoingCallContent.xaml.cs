using Core.Domain.Calls;
using Core.Public.Services;
using NewAppMaui.View.Layout;

namespace NewAppMaui.View.Pages.Content;

public partial class OutgoingCallContent : ContentView
{
    private readonly ICallsService _calls;
    private readonly IFriendCallsService _friendCalls;
    private readonly CallUiController _callController;
    private CancellationTokenSource? _cts;
    private string _friendId = string.Empty;
    private string _roomCode = string.Empty;
    private bool _answered;
    private bool _cancelled;

    public event Action? BackRequested;

    public OutgoingCallContent(CallUiController callController)
    {
        var services = ((App)Application.Current!).Services;
        _calls = services.GetRequiredService<ICallsService>();
        _friendCalls = services.GetRequiredService<IFriendCallsService>();
        _callController = callController;

        InitializeComponent();
    }

    public void Initialize(string friendId, string friendUsername)
    {
        _friendId = friendId;
        NameLabel.Text = friendUsername;
        AvatarLabel.Text = friendUsername.Length >= 2
            ? friendUsername[..2].ToUpperInvariant()
            : friendUsername.ToUpperInvariant();

        _cts = new CancellationTokenSource();
        _friendCalls.InviteResponse += OnInviteResponse;
        _calls.StateChanged += OnCallStateChanged;

        _ = StartCallAsync(_cts.Token);
    }

    public void Cleanup()
    {
        _friendCalls.InviteResponse -= OnInviteResponse;
        _calls.StateChanged -= OnCallStateChanged;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task StartCallAsync(CancellationToken ct)
    {
        try
        {
            if (_callController.IsInCall)
                await _callController.EndCallAsync("NewOutgoingCall");

            StatusLabel.Text = "Creating room...";
            var session = await _calls.CreateAsync(ct);
            _roomCode = session.RoomCode;

            StatusLabel.Text = "Ringing...";
            await _friendCalls.SendInviteAsync(_friendId, _roomCode, ct);

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                if (!_answered && !_cancelled)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        StatusLabel.Text = "No answer";
                        Spinner.IsVisible = false;
                        Spinner.IsRunning = false;
                    });
                    try { await _friendCalls.CancelAsync(_friendId, _roomCode, ct); } catch { }
                    try { await _calls.HangupAsync(ct); } catch { }
                }
            }, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Spinner.IsVisible = false;
                Spinner.IsRunning = false;
                ErrorLabel.Text = ex.Message;
                ErrorLabel.IsVisible = true;
                StatusLabel.Text = "Failed";
            });
        }
    }

    private void OnInviteResponse(object? sender, CallInviteResponseInfo info)
    {
        if (!string.Equals(info.RoomCode, _roomCode, StringComparison.OrdinalIgnoreCase))
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (info.Accepted)
            {
                _answered = true;
                StatusLabel.Text = "Connecting...";
            }
            else
            {
                _cancelled = true;
                Spinner.IsVisible = false;
                Spinner.IsRunning = false;

                var reason = string.IsNullOrWhiteSpace(info.Reason) ? "Declined" : info.Reason;
                StatusLabel.Text = reason;
                CancelButton.Text = "Back";
            }
        });
    }

    private void OnCallStateChanged(object? sender, CallState state)
    {
        if (state != CallState.Connected || !_answered)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var session = _calls.Current;
            if (session is null) return;

            var initial = session.Interlocutors
                .Select(x => new Core.Websockets.Messages.NoAuthCall.InterlocutorJoined
                {
                    Id = x.Id,
                    IpEndPoint = x.RemoteIp.ToString()
                })
                .ToArray();

            Cleanup();
            _callController.StartCall(session.RoomCode, initial);
        });
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        if (_cancelled || _answered)
        {
            Cleanup();
            BackRequested?.Invoke();
            return;
        }

        _cancelled = true;
        try
        {
            if (!string.IsNullOrEmpty(_roomCode))
                await _friendCalls.CancelAsync(_friendId, _roomCode);
            await _calls.HangupAsync();
        }
        catch { }

        Cleanup();
        BackRequested?.Invoke();
    }
}
