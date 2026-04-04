using Core.Public.Services;

namespace NewAppMaui.View.Pages.Content;

public partial class AddFriendContent : ContentView
{
    private readonly IFriendsService _friends;
    private readonly IAuthService _auth;

    public event Action? BackRequested;

    public AddFriendContent()
    {
        var services = ((App)Application.Current!).Services;
        _friends = services.GetRequiredService<IFriendsService>();
        _auth = services.GetRequiredService<IAuthService>();

        InitializeComponent();

        if (_auth.Username is not null && _auth.UserCode is not null)
            MyHandleLabel.Text = $"Your handle: {_auth.Username}#{_auth.UserCode}";
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var handle = HandleEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(handle))
        {
            ShowError("Enter a username#code");
            return;
        }

        ErrorLabel.IsVisible = false;
        SuccessLabel.IsVisible = false;
        SendButton.IsEnabled = false;

        try
        {
            await _friends.SendFriendRequestAsync(handle);

            SuccessLabel.IsVisible = true;
            HandleEntry.Text = "";
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private void ShowError(string msg)
    {
        ErrorLabel.Text = msg;
        ErrorLabel.IsVisible = true;
        SuccessLabel.IsVisible = false;
    }

    private async void OnMyHandleTapped(object? sender, TappedEventArgs e)
    {
        if (_auth.Username is null || _auth.UserCode is null) return;

        var handle = $"{_auth.Username}#{_auth.UserCode}";
        await Clipboard.Default.SetTextAsync(handle);

        MyHandleLabel.Text = "Copied!";
        await Task.Delay(1500);
        MyHandleLabel.Text = $"Your handle: {handle}";
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        BackRequested?.Invoke();
    }
}
