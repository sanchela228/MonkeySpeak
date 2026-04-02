using Core.Public.Services;
using NewAppMaui.View.Layout;
using NewAppMaui.View.Pages;

namespace NewAppMaui;

public partial class LoginPage : ContentPage
{
    private readonly IAuthService _auth;
    private readonly IServiceProvider _services;

    public LoginPage(IAuthService auth, IServiceProvider services)
    {
        _auth = auth;
        _services = services;
        InitializeComponent();
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Enter username and password");
            return;
        }

        ErrorLabel.IsVisible = false;
        LoginBtn.IsEnabled = false;
        Spinner.IsRunning = true;
        Spinner.IsVisible = true;

        try
        {
            await _auth.LoginAsync(username, password);

            if (Window is not null)
                Window.Page = _services.GetRequiredService<MainLayout>();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            LoginBtn.IsEnabled = true;
            Spinner.IsRunning = false;
            Spinner.IsVisible = false;
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (Window is not null)
            Window.Page = _services.GetRequiredService<ConnectionProfileSelectPage>();
    }
    
    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
