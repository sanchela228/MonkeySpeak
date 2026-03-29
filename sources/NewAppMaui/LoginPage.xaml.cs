using Core.Public.Services;

namespace NewAppMaui;

public partial class LoginPage : ContentPage
{
    private readonly IAuthService _auth;

    public LoginPage()
    {
        _auth = ((App)Application.Current!).Services.GetRequiredService<IAuthService>();
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

            await Shell.Current.GoToAsync("//MainPage");
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

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
