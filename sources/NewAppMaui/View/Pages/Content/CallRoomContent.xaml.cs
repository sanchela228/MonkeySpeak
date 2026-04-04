using Core.Domain.Media;
using Core.Public.Services;
using Core.Websockets;
using Core.Websockets.Messages.NoAuthCall;
using NewAppMaui.View.Components;
using NewAppMaui.View.Layout;

namespace NewAppMaui.View.Pages.Content;

public partial class CallRoomContent : ContentView
{
    private readonly IConnectionService _connection;
    private readonly ICallsService _calls;
    private readonly IAudioService _audio;
    private readonly IUserSettingsService _settings;
    private readonly CallUiController _callController;

    private readonly List<ParticipantVm> _participants = new();
    private string _roomCode = string.Empty;
    private bool _hasEverHadRemote;
    private bool _suppressDevicePickerEvents;

    public event Action<int>? ParticipantCountChanged;
    public event Action? AudioStateChanged;

    public CallRoomContent(CallUiController callController)
    {
        var services = ((App)Application.Current!).Services;
        _connection = services.GetRequiredService<IConnectionService>();
        _calls = services.GetRequiredService<ICallsService>();
        _audio = services.GetRequiredService<IAudioService>();
        _settings = services.GetRequiredService<IUserSettingsService>();
        _callController = callController;

        InitializeComponent();
    }

    public void InitializeRoom(string roomCode, IEnumerable<InterlocutorJoined>? initialParticipants)
    {
        _roomCode = roomCode ?? string.Empty;
        RoomCodeLabel.Text = _roomCode.ToUpperInvariant();

        if (initialParticipants is not null)
        {
            foreach (var p in initialParticipants)
                UpsertParticipant(p.Id, p.IpEndPoint);
        }

        if (_participants.Count > 0)
            _hasEverHadRemote = true;

        RefreshParticipantsUi();
    }

    public void Activate()
    {
        _connection.MessageReceived += OnMessageReceived;
        _connection.StateChanged += OnStateChanged;
        _calls.StateChanged += OnCallStateChanged;
        _calls.AvatarReceived += OnAvatarReceived;
        TryInitializeAudioAndPickers();
    }

    public void Deactivate()
    {
        _connection.MessageReceived -= OnMessageReceived;
        _connection.StateChanged -= OnStateChanged;
        _calls.StateChanged -= OnCallStateChanged;
        _calls.AvatarReceived -= OnAvatarReceived;
    }

    private void TryInitializeAudioAndPickers()
    {
        try
        {
            if (!_audio.IsInitialized)
                _audio.Initialize();

            _audio.RefreshDevices();
            PopulateDevicePickers();
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"Audio init/devices failed: {ex.Message}");
        }
    }

    private void PopulateDevicePickers()
    {
        try
        {
            _suppressDevicePickerEvents = true;

            var capList = _audio.CaptureDevices?.ToList() ?? new List<AudioDeviceInfo>();
            var pbList = _audio.PlaybackDevices?.ToList() ?? new List<AudioDeviceInfo>();

            CapturePicker.ItemsSource = capList;
            PlaybackPicker.ItemsSource = pbList;

            var prefCap = _settings.PreferredCaptureDeviceId;
            var prefPb = _settings.PreferredPlaybackDeviceId;

            CapturePicker.SelectedItem =
                (prefCap is not null ? capList.FirstOrDefault(d => d.Id == prefCap) : null)
                ?? capList.FirstOrDefault(d => d.IsDefault)
                ?? capList.FirstOrDefault();

            PlaybackPicker.SelectedItem =
                (prefPb is not null ? pbList.FirstOrDefault(d => d.Id == prefPb) : null)
                ?? pbList.FirstOrDefault(d => d.IsDefault)
                ?? pbList.FirstOrDefault();
        }
        catch { }
        finally
        {
            _suppressDevicePickerEvents = false;
        }
    }

    private void OnCapturePickerChanged(object? sender, EventArgs e)
    {
        if (_suppressDevicePickerEvents) return;
        try
        {
            if (CapturePicker.SelectedItem is AudioDeviceInfo dev)
            {
                _audio.SwitchCaptureDevice(dev.Id);
                _settings.PreferredCaptureDeviceId = dev.Id;
                _ = _settings.SaveAsync();
            }
        }
        catch (Exception ex) { Core.Logger.Warn($"SwitchCaptureDevice failed: {ex.Message}"); }
    }

    private void OnPlaybackPickerChanged(object? sender, EventArgs e)
    {
        if (_suppressDevicePickerEvents) return;
        try
        {
            if (PlaybackPicker.SelectedItem is AudioDeviceInfo dev)
            {
                _audio.SwitchPlaybackDevice(dev.Id);
                _settings.PreferredPlaybackDeviceId = dev.Id;
                _ = _settings.SaveAsync();
            }
        }
        catch (Exception ex) { Core.Logger.Warn($"SwitchPlaybackDevice failed: {ex.Message}"); }
    }

    private void OnStateChanged(object? sender, System.Data.ConnectionState e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = e == System.Data.ConnectionState.Open ? "Connected" : e.ToString();
        });
    }

    private void OnCallStateChanged(object? sender, Core.Domain.Calls.CallState state)
    {
        if (state is Core.Domain.Calls.CallState.Closed or Core.Domain.Calls.CallState.Failed)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_callController.IsInCall)
                    _ = _callController.EndCallAsync("SessionClosed");
            });
        }
    }

    private void OnMessageReceived(object? sender, string raw)
    {
        Context? ctx;
        try
        {
            ctx = System.Text.Json.JsonSerializer.Deserialize<Context>(raw,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return; }

        if (ctx is null) return;

        object? msg;
        try { msg = ctx.ToMessage(); }
        catch { return; }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LastMessageLabel.Text = raw;

            switch (msg)
            {
                case InterlocutorJoined joined:
                    UpsertParticipant(joined.Id, joined.IpEndPoint);
                    _hasEverHadRemote = true;
                    break;
                case InterlocutorLeft left:
                    RemoveParticipant(left.InterlocutorId);
                    break;
            }

            RefreshParticipantsUi();
            _ = MaybeAutoHangupAsync();
        });
    }

    private void UpsertParticipant(string id, string ipEndPoint)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var existing = _participants.FirstOrDefault(p => p.Id == id);
        if (existing is null)
            _participants.Add(new ParticipantVm { Id = id, IpEndPoint = ipEndPoint ?? "" });
        else
            existing.IpEndPoint = ipEndPoint ?? "";
    }

    private void RemoveParticipant(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var existing = _participants.FirstOrDefault(p => p.Id == id);
        if (existing is not null)
            _participants.Remove(existing);
    }

    private void RefreshParticipantsUi()
    {
        ParticipantsPanel.Children.Clear();
        NoParticipantsLabel.IsVisible = _participants.Count == 0;

        foreach (var p in _participants)
        {
            var row = new HorizontalStackLayout { Spacing = 8 };

            var avatar = new AvatarView();
            avatar.SetupInterlocutor(28, p.Id, p.Id);
            row.Children.Add(avatar);

            var label = new Label
            {
                Text = $"{p.Id}  •  {p.IpEndPoint}",
                FontSize = 13,
                TextColor = Color.FromArgb("#9ca3af"),
                VerticalOptions = LayoutOptions.Center
            };
            row.Children.Add(label);

            ParticipantsPanel.Children.Add(row);
        }

        ParticipantCountChanged?.Invoke(_participants.Count + 1);
    }

    private void OnAvatarReceived(string interlocutorId, byte[] data)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_participants.Any(p => p.Id == interlocutorId))
                RefreshParticipantsUi();
        });
    }

    public void RefreshAudioButtons()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MicButton.Text = _audio.IsMicrophoneEnabled ? "Mic: ON" : "Mic: OFF";
            PlaybackButton.Text = _audio.IsPlaybackEnabled ? "Sound: ON" : "Sound: OFF";
        });
    }

    private async Task MaybeAutoHangupAsync()
    {
        if (!_hasEverHadRemote || _participants.Count > 0) return;
        await _callController.EndCallAsync("Alone");
    }

    private void OnMicToggleClicked(object? sender, EventArgs e)
    {
        _callController.ToggleMic();
    }

    private void OnPlaybackToggleClicked(object? sender, EventArgs e)
    {
        _callController.ToggleVolume();
    }

    private async void OnHangupClicked(object? sender, EventArgs e)
    {
        await _callController.EndCallAsync("UserHangup");
    }

    private sealed class ParticipantVm
    {
        public string Id { get; set; } = string.Empty;
        public string IpEndPoint { get; set; } = string.Empty;
    }
}
