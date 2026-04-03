using Core.Domain.Friends;
using Core.Public.Services;

namespace NewAppMaui.View.Layout;

public partial class Sidebar : ContentView
{
    public event Action<string>? MenuItemSelected;
    public event Action? CollapseRequested;
    public event Action? CallHangupRequested;
    public event Action? CallMicToggled;
    public event Action? CallVolumeToggled;
    public event EventHandler<Friend>? FriendSelected;
    public event EventHandler? AddFriendRequested;
    public event EventHandler<FriendRequest>? AcceptFriendRequested;
    public event EventHandler? IncomingCallAccepted;
    public event EventHandler? IncomingCallRejected;
    public event EventHandler<FriendRequest>? RejectFriendRequested;

    private IDispatcherTimer? _callTimer;
    private DateTime _callStartTime;

    public Sidebar()
    {
        InitializeComponent();
        SetActiveItem("menu1");

        FriendsPanelView.FriendSelected += (_, f) => FriendSelected?.Invoke(this, f);
        FriendsPanelView.AddFriendRequested += (_, _) => AddFriendRequested?.Invoke(this, EventArgs.Empty);
        FriendsPanelView.AcceptRequested += (_, r) => AcceptFriendRequested?.Invoke(this, r);
        FriendsPanelView.RejectRequested += (_, r) => RejectFriendRequested?.Invoke(this, r);
    }

    public void InitializeFriends(IFriendsService friendsService)
    {
        FriendsPanelView.Initialize(friendsService);
    }

    public void SubscribeToProfileChanges(IAuthService auth, IUserSettingsService settings)
    {
        auth.UsernameChanged += (_, newName) => MainThread.BeginInvokeOnMainThread(() => SetUsername(newName));
        settings.AvatarChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => UpdateAvatar(settings));

        UpdateAvatar(settings);
    }

    public void SetUsername(string username)
    {
        UsernameLabel.Text = username;
        UserInitialsLabel.Text = username.Length >= 2
            ? username[..2].ToUpperInvariant()
            : username.ToUpperInvariant();
    }

    public void UpdateAvatar(IUserSettingsService settings)
    {
        if (!string.IsNullOrEmpty(settings.AvatarPath))
        {
            var fullPath = Path.Combine(settings.ProfileDirectory, settings.AvatarPath);
            if (File.Exists(fullPath))
            {
                UserAvatarImage.Source = ImageSource.FromFile(fullPath);
                UserAvatarImage.IsVisible = true;
                UserAvatarBorder.IsVisible = false;
                return;
            }
        }

        UserAvatarImage.IsVisible = false;
        UserAvatarBorder.IsVisible = true;
    }

    public void SetCallActive(bool active, string roomCode = "")
    {
        CallMenuItem.IsVisible = active;

        if (active)
        {
            CallRoomCodeLabel.Text = roomCode.ToUpperInvariant();
            CallParticipantsLabel.Text = "1 in call";
            _callStartTime = DateTime.Now;
            StartCallTimer();
        }
        else
        {
            StopCallTimer();
        }
    }

    public void UpdateParticipantCount(int count)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CallParticipantsLabel.Text = count == 1 ? "1 in call" : $"{count} in call";
        });
    }

    public void SyncMicState(bool enabled)
    {
        CallMicIcon.Source = enabled ? "micro.png" : "micro_muted.png";
    }

    public void SyncVolumeState(bool enabled)
    {
        CallVolumeIcon.Source = enabled ? "volume.png" : "volume_muted.png";
    }

    public void SetActiveItem(string itemKey)
    {
        MenuItem1.IsActive = itemKey == "menu1";
        MenuItem2.IsActive = itemKey == "menu2";
    }

    private void StartCallTimer()
    {
        StopCallTimer();
        _callTimer = Dispatcher.CreateTimer();
        _callTimer.Interval = TimeSpan.FromSeconds(1);
        _callTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _callStartTime;
            CallTimerLabel.Text = elapsed.Hours > 0
                ? elapsed.ToString(@"hh\:mm\:ss")
                : elapsed.ToString(@"mm\:ss");
        };
        _callTimer.Start();
    }

    private void StopCallTimer()
    {
        _callTimer?.Stop();
        _callTimer = null;
        CallTimerLabel.Text = "00:00";
    }

    private void OnMenu1Tapped(object? sender, EventArgs e)
    {
        SetActiveItem("menu1");
        MenuItemSelected?.Invoke("menu1");
    }

    private void OnMenu2Tapped(object? sender, EventArgs e)
    {
        SetActiveItem("menu2");
        MenuItemSelected?.Invoke("menu2");
    }

    private void OnCallTapped(object? sender, TappedEventArgs e)
    {
        MenuItemSelected?.Invoke("call");
    }

    private void OnCallMicTapped(object? sender, TappedEventArgs e)
    {
        CallMicToggled?.Invoke();
    }

    private void OnCallVolumeTapped(object? sender, TappedEventArgs e)
    {
        CallVolumeToggled?.Invoke();
    }

    private void OnCallHangupTapped(object? sender, TappedEventArgs e)
    {
        CallHangupRequested?.Invoke();
    }

    public void ShowIncomingCall(string callerName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IncomingCallerLabel.Text = $"{callerName}";
            IncomingCallWidget.IsVisible = true;
        });
    }

    public void HideIncomingCall()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IncomingCallWidget.IsVisible = false;
        });
    }

    private void OnIncomingAcceptClicked(object? sender, EventArgs e)
    {
        IncomingCallAccepted?.Invoke(this, EventArgs.Empty);
    }

    private void OnIncomingRejectClicked(object? sender, EventArgs e)
    {
        IncomingCallRejected?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        MenuItemSelected?.Invoke("settings");
    }

    private void OnCollapseTapped(object? sender, TappedEventArgs e)
    {
        CollapseRequested?.Invoke();
    }
}
