using Core.Domain.Friends;
using Core.Public.Services;
using Core.Websockets.Messages.NoAuthCall;
using NewAppMaui.View.Pages.Content;

namespace NewAppMaui.View.Layout;

public partial class MainLayout : ContentPage
{
    private readonly IAuthService _auth;
    private readonly IFriendsService _friends;
    private readonly IAudioService _audio;
    private readonly IUserSettingsService _settings;
    private readonly CallUiController _callController;
    private readonly FriendCallsUiCoordinator _friendCallsCoordinator;
    private readonly Dictionary<string, Func<Microsoft.Maui.Controls.View>> _contentFactories;
    private CallRoomContent? _callRoomContent;
    private bool _sidebarCollapsed;

    public event Action? IncomingCallAcceptRequested;
    public event Action? IncomingCallRejectRequested;

    public MainLayout(IAuthService auth, IFriendsService friends, IAudioService audio, IUserSettingsService settings, CallUiController callController, FriendCallsUiCoordinator friendCallsCoordinator)
    {
        _auth = auth;
        _friends = friends;
        _audio = audio;
        _settings = settings;
        _callController = callController;
        _friendCallsCoordinator = friendCallsCoordinator;
        _callController.AttachLayout(this);
        _friendCallsCoordinator.AttachLayout(this);

        InitializeComponent();

        _contentFactories = new()
        {
            ["menu1"] = () => new P2PCallTestPage(),
            ["menu2"] = () => new P2PCallTestPage2(),
        };

        SidebarView.MenuItemSelected += OnMenuItemSelected;
        SidebarView.CollapseRequested += () => SetSidebarCollapsed(true);
        SidebarView.CallHangupRequested += async () => await _callController.EndCallAsync("UserHangup");
        SidebarView.CallMicToggled += () => _callController.ToggleMic();
        SidebarView.CallVolumeToggled += () => _callController.ToggleVolume();
        SidebarView.FriendSelected += OnFriendSelected;
        SidebarView.AddFriendRequested += (_, _) => ShowAddFriend();
        SidebarView.AcceptFriendRequested += OnAcceptFriend;
        SidebarView.RejectFriendRequested += OnRejectFriend;
        SidebarView.IncomingCallAccepted += (_, _) => IncomingCallAcceptRequested?.Invoke();
        SidebarView.IncomingCallRejected += (_, _) => IncomingCallRejectRequested?.Invoke();

        if (_settings.SidebarCollapsed)
            SetSidebarCollapsed(true);

        NavigateTo("menu1");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_auth.IsAuthenticated && _auth.Username is not null)
            SidebarView.SetUsername(_auth.Username);

        SidebarView.InitializeFriends(_friends);
        SidebarView.SubscribeToProfileChanges(_auth, _settings);
        ApplySavedAudioSettings();
    }

    public void NavigateTo(string key)
    {
        if (key == "call" && _callRoomContent is not null)
        {
            SwitchToCallView();
            return;
        }

        if (_contentFactories.TryGetValue(key, out var factory))
        {
            SwitchToContentView();
            ContentArea.Content = factory();
            SidebarView.SetActiveItem(key);
        }
    }

    public async void ShowCreateRoom()
    {
        if (_callController.IsInCall)
            await _callController.EndCallAsync("NewRoom");

        var view = new CreateRoomContent();
        view.BackRequested += () => NavigateTo("menu1");
        view.RoomConnected += OnRoomConnected;
        SwitchToContentView();
        ContentArea.Content = view;
    }

    public async void ShowJoinRoom()
    {
        if (_callController.IsInCall)
            await _callController.EndCallAsync("NewRoom");

        var view = new JoinRoomContent();
        view.BackRequested += () => NavigateTo("menu1");
        view.RoomConnected += OnRoomConnected;
        SwitchToContentView();
        ContentArea.Content = view;
    }

    private void ShowAddFriend()
    {
        var view = new AddFriendContent();
        view.BackRequested += () => NavigateTo("menu1");
        SwitchToContentView();
        ContentArea.Content = view;
        SidebarView.SetActiveItem("");
    }

    private void OnFriendSelected(object? sender, Friend friend)
    {
        var view = new FriendProfileContent();
        view.SetFriend(friend);
        view.BackRequested += () => NavigateTo("menu1");
        view.CallRequested += (friendId, friendUsername) => ShowOutgoingCall(friendId, friendUsername);
        SwitchToContentView();
        ContentArea.Content = view;
        SidebarView.SetActiveItem("");
    }

    private async void OnAcceptFriend(object? sender, FriendRequest request)
    {
        try
        {
            await _friends.AcceptAsync(request.FriendshipId);
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"Accept friend failed: {ex.Message}");
        }
    }

    private async void OnRejectFriend(object? sender, FriendRequest request)
    {
        try
        {
            await _friends.RejectAsync(request.FriendshipId);
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"Reject friend failed: {ex.Message}");
        }
    }

    public void ShowOutgoingCall(string friendId, string friendUsername)
    {
        _friendCallsCoordinator.SetCurrentFriendUserId(friendId);
        var view = new OutgoingCallContent(_callController);
        view.Initialize(friendId, friendUsername);
        view.BackRequested += () =>
        {
            view.Cleanup();
            NavigateTo("menu1");
        };
        SwitchToContentView();
        ContentArea.Content = view;
        SidebarView.SetActiveItem("");
    }

    public void ShowIncomingCall(string callerName)
    {
        SidebarView.ShowIncomingCall(callerName);
    }

    public void HideIncomingCall()
    {
        SidebarView.HideIncomingCall();
    }

    private void OnRoomConnected(string roomCode, InterlocutorJoined[] initial)
    {
        _callController.StartCall(roomCode, initial);
    }

    public void ShowCallView(string roomCode, InterlocutorJoined[] initialParticipants)
    {
        _callRoomContent = new CallRoomContent(_callController);
        _callRoomContent.ParticipantCountChanged += count =>
            SidebarView.UpdateParticipantCount(count);
        _callRoomContent.AudioStateChanged += SyncAudioStates;
        _callRoomContent.InitializeRoom(roomCode, initialParticipants);
        _callRoomContent.Activate();

        CallViewContainer.Content = _callRoomContent;
        SidebarView.SetCallActive(true, roomCode);

        SwitchToCallView();
    }

    public void HideCallView()
    {
        if (_callRoomContent is not null)
        {
            _callRoomContent.Deactivate();
            _callRoomContent = null;
        }

        CallViewContainer.Content = null;
        SidebarView.SetCallActive(false);

        SwitchToContentView();
        NavigateTo("menu1");
    }

    public void SyncAudioStates()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SidebarView.SyncMicState(_callController.IsMicEnabled);
            SidebarView.SyncVolumeState(_callController.IsVolumeEnabled);
            _callRoomContent?.RefreshAudioButtons();
        });
    }

    private void SwitchToCallView()
    {
        ContentArea.IsVisible = false;
        CallViewContainer.IsVisible = true;
        SidebarView.SetActiveItem("");
    }

    private void SwitchToContentView()
    {
        CallViewContainer.IsVisible = false;
        ContentArea.IsVisible = true;
    }

    private void SetSidebarCollapsed(bool collapsed)
    {
        _sidebarCollapsed = collapsed;
        _settings.SidebarCollapsed = collapsed;
        _ = _settings.SaveAsync();

        SidebarView.IsVisible = !collapsed;
        SidebarSeparator.IsVisible = !collapsed;
        ExpandButton.IsVisible = collapsed;

        RootGrid.ColumnDefinitions[0].Width = collapsed
            ? new GridLength(0)
            : new GridLength(260);
    }

    private void ApplySavedAudioSettings()
    {
        try
        {
            if (!_audio.IsInitialized)
                _audio.Initialize();

            _audio.MicrophoneVolume = (int)(_settings.MicrophoneVolume * 100);
            _audio.PlaybackVolume = (int)(_settings.MasterVolume * 100);
            _audio.IsNoiseSuppressionEnabled = _settings.NoiseSuppressionEnabled;

            if (_settings.PreferredCaptureDeviceId is not null)
            {
                try { _audio.SwitchCaptureDevice(_settings.PreferredCaptureDeviceId); } catch { }
            }
            if (_settings.PreferredPlaybackDeviceId is not null)
            {
                try { _audio.SwitchPlaybackDevice(_settings.PreferredPlaybackDeviceId); } catch { }
            }
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"ApplySavedAudioSettings failed: {ex.Message}");
        }
    }

    private void OnExpandTapped(object? sender, TappedEventArgs e)
    {
        SetSidebarCollapsed(false);
    }

    private void ShowSettings()
    {
        var view = new SettingsContent();
        SwitchToContentView();
        ContentArea.Content = view;
        SidebarView.SetActiveItem("");
    }

    private void OnMenuItemSelected(string key)
    {
        if (key == "settings")
            ShowSettings();
        else
            NavigateTo(key);
    }
}
