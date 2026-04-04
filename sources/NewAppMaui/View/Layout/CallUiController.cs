using Core.Domain.Calls;
using Core.Public.Services;
using NewAppMaui.Services;

namespace NewAppMaui.View.Layout;

public class CallUiController
{
    private readonly ICallsService _calls;
    private readonly IAudioService _audio;
    private readonly UiSoundService _uiSound;
    private MainLayout? _layout;
    private bool _ending;

    public bool IsInCall { get; private set; }
    public string RoomCode { get; private set; } = string.Empty;

    public event Action? CallStarted;
    public event Action? CallEnded;

    public CallUiController(ICallsService calls, IAudioService audio, UiSoundService uiSound)
    {
        _calls = calls;
        _audio = audio;
        _uiSound = uiSound;

        _calls.StateChanged += OnCallStateChanged;
    }

    public void AttachLayout(MainLayout layout)
    {
        _layout = layout;
    }

    public void StartCall(string roomCode, Core.Websockets.Messages.NoAuthCall.InterlocutorJoined[] initialParticipants)
    {
        if (IsInCall) return;

        _ending = false;
        IsInCall = true;
        RoomCode = roomCode;

        _audio.IsMicrophoneEnabled = true;
        _audio.IsPlaybackEnabled = true;

        CallStarted?.Invoke();
        _uiSound.PlayConnect();

        _layout?.ShowCallView(roomCode, initialParticipants);
        _layout?.SyncAudioStates();
    }

    public async Task EndCallAsync(string reason = "UserHangup")
    {
        if (!IsInCall || _ending) return;

        _ending = true;
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

        _ending = false;

        _uiSound.PlayDisconnect();
        CallEnded?.Invoke();
        MainThread.BeginInvokeOnMainThread(() => _layout?.HideCallView());
    }

    public void ToggleMic()
    {
        _audio.IsMicrophoneEnabled = !_audio.IsMicrophoneEnabled;

        if (_audio.IsMicrophoneEnabled)
            _uiSound.PlayMicOn();
        else
            _uiSound.PlayMicOff();

        _layout?.SyncAudioStates();
    }

    public void ToggleVolume()
    {
        _audio.IsPlaybackEnabled = !_audio.IsPlaybackEnabled;
        _layout?.SyncAudioStates();
    }

    public bool IsMicEnabled => _audio.IsInitialized && _audio.IsMicrophoneEnabled;
    public bool IsVolumeEnabled => _audio.IsInitialized && _audio.IsPlaybackEnabled;

    private void OnCallStateChanged(object? sender, CallState state)
    {
        if (state == CallState.Closed && IsInCall && !_ending)
        {
            _ = EndCallAsync("Disconnected");
        }
    }
}
