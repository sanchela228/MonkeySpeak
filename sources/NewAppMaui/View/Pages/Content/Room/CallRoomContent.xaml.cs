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
    private readonly IFriendsService _friends;
    private readonly CallUiController _callController;

    private readonly List<ParticipantVm> _participants = new();
    private string _roomCode = string.Empty;
    private bool _hasEverHadRemote;
    private bool _suppressDevicePickerEvents;
    private ChatContent? _chatContent;
    private bool _chatVisible;
    private int _unreadCount;

    public event Action<int>? ParticipantCountChanged;
    public event Action? AudioStateChanged;

    public CallRoomContent(CallUiController callController)
    {
        var services = ((App)Application.Current!).Services;
        _connection = services.GetRequiredService<IConnectionService>();
        _calls = services.GetRequiredService<ICallsService>();
        _audio = services.GetRequiredService<IAudioService>();
        _settings = services.GetRequiredService<IUserSettingsService>();
        _friends = services.GetRequiredService<IFriendsService>();
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
        _calls.MuteStateChanged += OnRemoteMuteStateChanged;
        _calls.DisplayNameReceived += OnDisplayNameReceived;
        _calls.ChatMessageReceived += OnChatMessageWhileClosed;

        _chatContent = new ChatContent();
        _chatContent.Activate();
        ChatContainer.Content = _chatContent;

        TryInitializeAudioAndPickers();
    }

    public void Deactivate()
    {
        _chatContent?.Deactivate();
        _chatContent = null;
        _chatVisible = false;
        _unreadCount = 0;

        _connection.MessageReceived -= OnMessageReceived;
        _connection.StateChanged -= OnStateChanged;
        _calls.StateChanged -= OnCallStateChanged;
        _calls.AvatarReceived -= OnAvatarReceived;
        _calls.MuteStateChanged -= OnRemoteMuteStateChanged;
        _calls.DisplayNameReceived -= OnDisplayNameReceived;
        _calls.ChatMessageReceived -= OnChatMessageWhileClosed;
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

    private string? ResolveParticipantName(string id)
    {
        var it = _calls.Current?.Interlocutors?.FirstOrDefault(x => x.Id == id);
        if (!string.IsNullOrEmpty(it?.DisplayName))
            return it.DisplayName;

        var friend = _friends.Friends?.FirstOrDefault(f => f.UserId == id);
        if (friend is not null)
            return friend.Username;

        return null;
    }

    private void RefreshParticipantsUi()
    {
        foreach (var p in _participants)
        {
            if (string.IsNullOrEmpty(p.Name))
                p.Name = ResolveParticipantName(p.Id);
        }

        ParticipantsPanel.Children.Clear();
        NoParticipantsLabel.IsVisible = _participants.Count == 0;

        const int cols = 2;
        var items = _participants.ToList();

        for (var i = 0; i < items.Count; i += cols)
        {
            var chunk = items.Skip(i).Take(cols).ToList();

            var row = new HorizontalStackLayout
            {
                Spacing = 24,
                HorizontalOptions = LayoutOptions.Center
            };

            foreach (var p in chunk)
                row.Children.Add(CreateParticipantCard(p));

            ParticipantsPanel.Children.Add(row);
        }

        ParticipantCountChanged?.Invoke(_participants.Count + 1);
    }

    private static Microsoft.Maui.Controls.View CreateParticipantCard(ParticipantVm p)
    {
        var card = new VerticalStackLayout
        {
            Spacing = 10,
            WidthRequest = 160,
            Padding = new Thickness(4, 8)
        };

        var avatar = new AvatarView();
        avatar.SetupInterlocutor(144, p.DisplayLabel, p.Id);
        avatar.HorizontalOptions = LayoutOptions.Center;

        var avatarContainer = new Grid
        {
            WidthRequest = 144,
            HeightRequest = 144,
            HorizontalOptions = LayoutOptions.Center
        };
        avatarContainer.Children.Add(avatar);

        if (p.IsMuted)
        {
            var muteBadge = new Border
            {
                WidthRequest = 36,
                HeightRequest = 36,
                StrokeThickness = 0,
                BackgroundColor = Color.FromArgb("#CC000000"),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 4, 4),
                Content = new Image
                {
                    Source = "micro_muted.png",
                    WidthRequest = 18,
                    HeightRequest = 18,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            muteBadge.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 };
            avatarContainer.Children.Add(muteBadge);
        }

        card.Children.Add(avatarContainer);

        card.Children.Add(new Label
        {
            Text = p.DisplayLabel,
            FontSize = 14,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation
        });

        return card;
    }

    private void OnAvatarReceived(string interlocutorId, byte[] data)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_participants.Any(p => p.Id == interlocutorId))
                RefreshParticipantsUi();
        });
    }

    private void OnRemoteMuteStateChanged(string interlocutorId, bool isMuted)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var p = _participants.FirstOrDefault(x => x.Id == interlocutorId);
            if (p is null) return;
            p.IsMuted = isMuted;
            RefreshParticipantsUi();
        });
    }

    private void OnDisplayNameReceived(string interlocutorId, string name)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var p = _participants.FirstOrDefault(x => x.Id == interlocutorId);
            if (p is null) return;
            p.Name = name;
            RefreshParticipantsUi();
        });
    }

    public void RefreshAudioButtons()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var micOn = _audio.IsMicrophoneEnabled;
            MicIcon.Source = micOn ? "micro.png" : "micro_muted.png";
            MicBorder.BackgroundColor = micOn
                ? Color.FromArgb("#333333")
                : Color.FromArgb("#555555");
            MicLabel.Text = micOn ? "Microphone" : "Mic off";

            var soundOn = _audio.IsPlaybackEnabled;
            SoundIcon.Source = soundOn ? "volume.png" : "volume_muted.png";
            SoundBorder.BackgroundColor = soundOn
                ? Color.FromArgb("#333333")
                : Color.FromArgb("#555555");
            SoundLabel.Text = soundOn ? "Sound" : "No sound";
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

    private void OnChatToggleClicked(object? sender, EventArgs e)
    {
        ToggleChat(!_chatVisible);
    }

    private void ToggleChat(bool show)
    {
        _chatVisible = show;

        if (_chatVisible)
        {
            ParticipantsScroll.IsVisible = false;
            NoParticipantsLabel.IsVisible = false;

            Grid.SetRow(ControlsLayout, 1);
            ControlsLayout.Padding = new Thickness(0, 12, 0, 12);
            MainGrid.RowDefinitions[1] = new RowDefinition(GridLength.Auto);
            MainGrid.RowDefinitions[2] = new RowDefinition(GridLength.Star);

            ChatContainer.IsVisible = true;
            ChatBorder.BackgroundColor = Color.FromArgb("#555555");

            _unreadCount = 0;
            UnreadBadge.IsVisible = false;
        }
        else
        {
            ChatContainer.IsVisible = false;

            Grid.SetRow(ControlsLayout, 2);
            ControlsLayout.Padding = new Thickness(0, 16, 0, 28);
            MainGrid.RowDefinitions[1] = new RowDefinition(GridLength.Star);
            MainGrid.RowDefinitions[2] = new RowDefinition(GridLength.Auto);

            ParticipantsScroll.IsVisible = true;
            RefreshParticipantsUi();
            ChatBorder.BackgroundColor = Color.FromArgb("#333333");
        }
    }

    private void OnChatMessageWhileClosed(string interlocutorId, Core.Application.Calls.Networking.ChatMessage msg)
    {
        if (_chatVisible) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _unreadCount++;
            UnreadCountLabel.Text = _unreadCount > 9 ? "9+" : _unreadCount.ToString();
            UnreadBadge.IsVisible = true;
        });
    }

    private async void OnHangupClicked(object? sender, EventArgs e)
    {
        await _callController.EndCallAsync("UserHangup");
    }

    private sealed class ParticipantVm
    {
        public string Id { get; set; } = string.Empty;
        public string IpEndPoint { get; set; } = string.Empty;
        public bool IsMuted { get; set; }
        public string? Name { get; set; }
        public string DisplayLabel => !string.IsNullOrEmpty(Name) ? Name : Id;
    }
}
