namespace NewAppMaui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(CreateRoomPage), typeof(CreateRoomPage));
        Routing.RegisterRoute(nameof(JoinRoomPage), typeof(JoinRoomPage));
        Routing.RegisterRoute(nameof(CallRoomPage), typeof(CallRoomPage));
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
    }
}