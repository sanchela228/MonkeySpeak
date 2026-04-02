using Core.Public.Services;
using Core.Websockets.Messages.NoAuthCall;
using NewAppMaui.View.Pages.Content;

namespace NewAppMaui.View.Layout;

public partial class MainLayout : ContentPage
{
    private readonly IAuthService _auth;
    private readonly IAudioService _audio;
    private readonly CallUiController _callController;
    private readonly Dictionary<string, Func<Microsoft.Maui.Controls.View>> _contentFactories;
    private CallRoomContent? _callRoomContent;
    private bool _sidebarCollapsed;
    private bool _showingCall;

    public MainLayout(IAuthService auth, IAudioService audio, CallUiController callController)
    {
        _auth = auth;
        _audio = audio;
        _callController = callController;
        _callController.AttachLayout(this);

        InitializeComponent();

        _contentFactories = new()
        {
            ["menu1"] = () => new P2PCallTestPage(),
            ["menu2"] = () => new P2PCallTestPage2(),
        };

        SidebarView.MenuItemSelected += OnMenuItemSelected;
        SidebarView.CollapseRequested += () => SetSidebarCollapsed(true);
        SidebarView.CallHangupRequested += () => _ = _callController.EndCallAsync("UserHangup");
        SidebarView.CallMicToggled += OnSidebarMicToggled;
        SidebarView.CallVolumeToggled += OnSidebarVolumeToggled;

        NavigateTo("menu1");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_auth.IsAuthenticated && _auth.Username is not null)
            SidebarView.SetUsername(_auth.Username);
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

    public void ShowCreateRoom()
    {
        if (_callController.IsInCall)
            _ = _callController.EndCallAsync("NewRoom");

        var view = new CreateRoomContent();
        view.BackRequested += () => NavigateTo("menu1");
        view.RoomConnected += OnRoomConnected;
        SwitchToContentView();
        ContentArea.Content = view;
    }

    public void ShowJoinRoom()
    {
        if (_callController.IsInCall)
            _ = _callController.EndCallAsync("NewRoom");

        var view = new JoinRoomContent();
        view.BackRequested += () => NavigateTo("menu1");
        view.RoomConnected += OnRoomConnected;
        SwitchToContentView();
        ContentArea.Content = view;
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
        _callRoomContent.MicToggled += OnCallMicToggledFromContent;
        _callRoomContent.VolumeToggled += OnCallVolumeToggledFromContent;
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

    private void OnSidebarMicToggled()
    {
        _audio.IsMicrophoneEnabled = !_audio.IsMicrophoneEnabled;
        _callRoomContent?.RefreshMicButton();
    }

    private void OnSidebarVolumeToggled()
    {
        _audio.IsPlaybackEnabled = !_audio.IsPlaybackEnabled;
        _callRoomContent?.RefreshPlaybackButton();
    }

    private void OnCallMicToggledFromContent()
    {
        SidebarView.SyncMicState(_audio.IsMicrophoneEnabled);
    }

    private void OnCallVolumeToggledFromContent()
    {
        SidebarView.SyncVolumeState(_audio.IsPlaybackEnabled);
    }

    private void SwitchToCallView()
    {
        _showingCall = true;
        ContentArea.IsVisible = false;
        CallViewContainer.IsVisible = true;
        SidebarView.SetActiveItem("");
    }

    private void SwitchToContentView()
    {
        _showingCall = false;
        CallViewContainer.IsVisible = false;
        ContentArea.IsVisible = true;
    }

    private void SetSidebarCollapsed(bool collapsed)
    {
        _sidebarCollapsed = collapsed;

        SidebarView.IsVisible = !collapsed;
        SidebarSeparator.IsVisible = !collapsed;
        ExpandButton.IsVisible = collapsed;

        RootGrid.ColumnDefinitions[0].Width = collapsed
            ? new GridLength(0)
            : new GridLength(260);
    }

    private void OnExpandTapped(object? sender, TappedEventArgs e)
    {
        SetSidebarCollapsed(false);
    }

    private void OnMenuItemSelected(string key)
    {
        NavigateTo(key);
    }
}
