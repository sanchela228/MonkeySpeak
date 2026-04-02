using Core.Public.Services;
using NewAppMaui.View.Pages.Content;

namespace NewAppMaui.View.Layout;

public partial class MainLayout : ContentPage
{
    private readonly IAuthService _auth;
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, Func<Microsoft.Maui.Controls.View>> _contentFactories;
    private bool _sidebarCollapsed;

    public MainLayout(IAuthService auth, IServiceProvider services)
    {
        _auth = auth;
        _services = services;

        InitializeComponent();

        _contentFactories = new()
        {
            ["menu1"] = () => new P2PCallTestPage(),
            ["menu2"] = () => new P2PCallTestPage2(),
        };

        SidebarView.MenuItemSelected += OnMenuItemSelected;
        SidebarView.CollapseRequested += () => SetSidebarCollapsed(true);

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
        if (_contentFactories.TryGetValue(key, out var factory))
        {
            ContentArea.Content = factory();
            SidebarView.SetActiveItem(key);
        }
    }

    public void ShowCreateRoom()
    {
        var view = new CreateRoomContent();
        view.BackRequested += () => NavigateTo("menu1");
        view.RoomConnected += OnRoomConnected;
        ContentArea.Content = view;
    }

    public void ShowJoinRoom()
    {
        var view = new JoinRoomContent();
        view.BackRequested += () => NavigateTo("menu1");
        view.RoomConnected += OnRoomConnected;
        ContentArea.Content = view;
    }

    private void OnRoomConnected(string roomCode, Core.Websockets.Messages.NoAuthCall.InterlocutorJoined[] initial)
    {
        // пока открываем CallRoomPage как модал (старое поведение)
        var page = _services.GetRequiredService<CallRoomPage>();
        page.InitializeRoom(roomCode, initial);
        _ = Navigation.PushModalAsync(new NavigationPage(page));
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
