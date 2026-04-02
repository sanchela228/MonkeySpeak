using Core.Domain.Calls;
using Core.Public.Services;

namespace NewAppMaui.View.Layout;

public class CallUiController
{
    private readonly ICallsService _calls;
    private readonly IAudioService _audio;
    private MainLayout? _layout;

    public bool IsInCall { get; private set; }
    public string RoomCode { get; private set; } = string.Empty;

    public event Action? CallStarted;
    public event Action? CallEnded;

    public CallUiController(ICallsService calls, IAudioService audio)
    {
        _calls = calls;
        _audio = audio;

        _calls.StateChanged += OnCallStateChanged;
    }

    public void AttachLayout(MainLayout layout)
    {
        _layout = layout;
    }

    public void StartCall(string roomCode, Core.Websockets.Messages.NoAuthCall.InterlocutorJoined[] initialParticipants)
    {
        if (IsInCall) return;

        IsInCall = true;
        RoomCode = roomCode;
        CallStarted?.Invoke();

        _layout?.ShowCallView(roomCode, initialParticipants);
    }

    public async Task EndCallAsync(string reason = "UserHangup")
    {
        if (!IsInCall) return;

        IsInCall = false;
        RoomCode = string.Empty;

        try
        {
            await _calls.HangupAsync();
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"CallUiController.EndCall hangup error: {ex.Message}");
        }

        CallEnded?.Invoke();
        MainThread.BeginInvokeOnMainThread(() => _layout?.HideCallView());
    }

    private void OnCallStateChanged(object? sender, CallState state)
    {
        if (state == CallState.Disconnected && IsInCall)
        {
            _ = EndCallAsync("Disconnected");
        }
    }
}
