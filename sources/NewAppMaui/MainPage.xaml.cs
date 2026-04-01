using Core.Public.Configurations;
using Core.Public.Services;

namespace NewAppMaui;

public partial class MainPage : ContentPage
{
    private readonly IConnectionService _connection;
    private readonly IAuthService _auth;

    public MainPage()
    {
        _connection = ((App)Application.Current!).Services.GetRequiredService<IConnectionService>();
        _auth = ((App)Application.Current!).Services.GetRequiredService<IAuthService>();
        InitializeComponent();

        _connection.StateChanged += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateUi);
        _connection.ServerSettingsReceived += OnServerSettingsReceived;
        _auth.Authenticated += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateUi);
        _auth.LoggedOut += (_, _) => MainThread.BeginInvokeOnMainThread(TryNavigateToAuthIfNeeded);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        TryNavigateToAuthIfNeeded();
        UpdateUi();
    }

    private void TryNavigateToAuthIfNeeded()
    {
        if (_auth.IsAuthenticated) return;

        var settings = _connection.ServerSettings;
        if (settings is null) return; // wait for ServerSettingsReceived

        if (settings.AuthMode == AuthMode.Password)
        {
            _ = Shell.Current.GoToAsync(nameof(LoginPage));
        }
        else // KeyPair
        {
            _ = _auth.AuthenticateAsync();
        }
    }

    private void OnServerSettingsReceived(object? sender, Core.Domain.BackendSettings settings)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_auth.IsAuthenticated)
                TryNavigateToAuthIfNeeded();
        });
    }

    private void UpdateUi()
    {
        ConnectionStatusLabel.Text = _connection.State.ToString();

        var connected = _connection.State == System.Data.ConnectionState.Open;

        AuthStatusLabel.Text = _auth.IsAuthenticated
            ? $"Authenticated: {_auth.Username} ({_auth.UserId})"
            : "Not authenticated";

        CreateConnectionBtn.IsVisible = connected && _auth.IsAuthenticated;
        ConnectBtn.IsVisible = connected && _auth.IsAuthenticated;
        FriendsBtn.IsVisible = connected && _auth.IsAuthenticated;

        var err = _connection.LastError;
        ConnectionErrorLabel.Text = err?.Message ?? string.Empty;
        ConnectionErrorLabel.IsVisible = !connected && err is not null;
    }

    private async void OnCreateConnectionClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CreateRoomPage));
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(JoinRoomPage));
    }

    private async void OnChangeConnectionClicked(object? sender, EventArgs e)
    {
        var page = ((App)Application.Current!).Services.GetRequiredService<ConnectionProfileSelectPage>();
        await Navigation.PushModalAsync(page);
    }

    private async void OnFriendsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(FriendsPage));
    }
}
