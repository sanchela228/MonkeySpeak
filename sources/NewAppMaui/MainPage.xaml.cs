namespace NewAppMaui;

public partial class MainPage : ContentPage
{
    private readonly Core.Public.Services.IConnectionService _connection;

    public MainPage()
    {
        _connection = ((App)Application.Current!).Services.GetRequiredService<Core.Public.Services.IConnectionService>();
        InitializeComponent();

        _connection.StateChanged += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateUi);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateUi();
    }

    private void UpdateUi()
    {
        ConnectionStatusLabel.Text = _connection.State.ToString();

        var connected = _connection.State == System.Data.ConnectionState.Open;

        CreateConnectionBtn.IsVisible = connected;
        ConnectBtn.IsVisible = connected;

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
        await Navigation.PushModalAsync(new NavigationPage(page));
    }
}