using Core.Public.Configurations;
using Core.Public.Services;
using NewAppMaui.View.Layout;

namespace NewAppMaui.View.Pages;

public partial class ConnectingPage : ContentPage
{
    private readonly IConnectionService _connection;
    private readonly IAuthService _auth;
    private readonly IServiceProvider _services;
    private CancellationTokenSource? _cts;

    public ConnectingPage(
        IConnectionService connection,
        IAuthService auth,
        IServiceProvider services)
    {
        _connection = connection;
        _auth = auth;
        _services = services;

        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await StartConnectionFlow();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _cts?.Cancel();
    }

    private async Task StartConnectionFlow()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        ShowLoading("Connecting...", "Establishing WebSocket connection");

        try
        {
            await _connection.ConnectAsync(ct);

            if (_connection.State != System.Data.ConnectionState.Open)
            {
                ShowError(_connection.LastError?.Message ?? "Failed to connect to server");
                return;
            }

            ShowLoading("Connected", "Waiting for server settings...");

            var settings = _connection.ServerSettings;
            if (settings is null)
            {
                settings = await WaitForServerSettings(ct);
                if (settings is null)
                {
                    ShowError("Server did not respond with settings");
                    return;
                }
            }

            ShowLoading("Authenticating...", "");

            if (settings.AuthMode == AuthMode.Password)
            {
                GoToLoginPage();
                return;
            }

            await _auth.AuthenticateAsync(ct);

            if (!_auth.IsAuthenticated)
            {
                ShowError("Authentication failed");
                return;
            }

            ShowLoading("Ready", $"Welcome, {_auth.Username}");
            await Task.Delay(300, ct);

            GoToMainLayout();
        }
        catch (OperationCanceledException)
        {
            // page closed
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async Task<Core.Domain.BackendSettings?> WaitForServerSettings(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<Core.Domain.BackendSettings>();
        ct.Register(() => tcs.TrySetCanceled());

        void Handler(object? s, Core.Domain.BackendSettings settings) => tcs.TrySetResult(settings);
        _connection.ServerSettingsReceived += Handler;

        try
        {
            if (_connection.ServerSettings is { } existing)
                return existing;

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            linked.Token.Register(() => tcs.TrySetCanceled());

            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _connection.ServerSettingsReceived -= Handler;
        }
    }

    private void ShowLoading(string status, string subStatus)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingPanel.IsVisible = true;
            ErrorPanel.IsVisible = false;
            Spinner.IsRunning = true;
            StatusLabel.Text = status;
            SubStatusLabel.Text = subStatus;
        });
    }

    private void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingPanel.IsVisible = false;
            ErrorPanel.IsVisible = true;
            ErrorLabel.Text = message;
        });
    }

    private void GoToMainLayout()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Window is not null)
                Window.Page = _services.GetRequiredService<MainLayout>();
        });
    }

    private void GoToLoginPage()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var loginPage = _services.GetRequiredService<LoginPage>();
            if (Window is not null)
                await Navigation.PushModalAsync(new NavigationPage(loginPage));
        });
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await StartConnectionFlow();
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        if (Window is not null)
            Window.Page = _services.GetRequiredService<ConnectionProfileSelectPage>();
    }

    private void OnChangeServerClicked(object? sender, EventArgs e)
    {
        if (Window is not null)
            Window.Page = _services.GetRequiredService<ConnectionProfileSelectPage>();
    }
}
