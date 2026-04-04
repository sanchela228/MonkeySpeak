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

    public Sidebar()
    {
        InitializeComponent();
        SetActiveItem("menu1");

        IncomingCallView.Accepted += (_, _) => IncomingCallAccepted?.Invoke(this, EventArgs.Empty);
        IncomingCallView.Rejected += (_, _) => IncomingCallRejected?.Invoke(this, EventArgs.Empty);

        ActiveCallView.CallTapped += () => MenuItemSelected?.Invoke("call");
        ActiveCallView.MicToggled += () => CallMicToggled?.Invoke();
        ActiveCallView.VolumeToggled += () => CallVolumeToggled?.Invoke();
        ActiveCallView.HangupRequested += () => CallHangupRequested?.Invoke();

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

    public void SetCallActive(bool active, string roomCode = "") =>
        ActiveCallView.SetActive(active, roomCode);

    public void UpdateParticipantCount(int count) =>
        ActiveCallView.UpdateParticipantCount(count);

    public void SyncMicState(bool enabled) =>
        ActiveCallView.SyncMicState(enabled);

    public void SyncVolumeState(bool enabled) =>
        ActiveCallView.SyncVolumeState(enabled);

    public void ShowIncomingCall(string callerName) =>
        IncomingCallView.Show(callerName);

    public void HideIncomingCall() =>
        IncomingCallView.Hide();

    public void SetActiveItem(string itemKey)
    {
        MenuItem1.IsActive = itemKey == "menu1";
        MenuItem2.IsActive = itemKey == "menu2";
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

    private void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        MenuItemSelected?.Invoke("settings");
    }

    private void OnCollapseTapped(object? sender, TappedEventArgs e)
    {
        CollapseRequested?.Invoke();
    }
}
