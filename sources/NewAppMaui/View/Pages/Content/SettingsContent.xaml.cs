using Core.Public.Services;

namespace NewAppMaui.View.Pages.Content;

public partial class SettingsContent : ContentView
{
    private readonly IAuthService _auth;
    private readonly IUserSettingsService _settings;

    public SettingsContent()
    {
        var services = ((App)Application.Current!).Services;
        _auth = services.GetRequiredService<IAuthService>();
        _settings = services.GetRequiredService<IUserSettingsService>();

        InitializeComponent();
        LoadCurrentState();
    }

    private void LoadCurrentState()
    {
        var name = _auth.Username ?? "Unknown";
        UsernameEntry.Text = name;
        AvatarInitials.Text = name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
        UsernameHintLabel.Text = $"Your handle: {name}#{_auth.UserCode}";
        UserIdLabel.Text = $"User ID: {_auth.UserId}";
        UserCodeLabel.Text = $"Code: {_auth.UserCode}";
        ProfilePathLabel.Text = $"Profile: {_settings.ProfileName} ({_settings.ProfileDirectory})";

        LoadAvatar();
    }

    private void LoadAvatar()
    {
        if (!string.IsNullOrEmpty(_settings.AvatarPath))
        {
            var fullPath = Path.Combine(_settings.ProfileDirectory, _settings.AvatarPath);
            if (File.Exists(fullPath))
            {
                AvatarImage.Source = ImageSource.FromFile(fullPath);
                AvatarImage.IsVisible = true;
                AvatarBorder.IsVisible = false;
                RemoveAvatarBtn.IsVisible = true;
                return;
            }
        }

        AvatarImage.IsVisible = false;
        AvatarBorder.IsVisible = true;
        RemoveAvatarBtn.IsVisible = false;
    }

    private async void OnChangeAvatarClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select avatar image",
                FileTypes = FilePickerFileType.Images
            });

            if (result is null) return;

            var destPath = Path.Combine(_settings.ProfileDirectory, "avatar.png");

            await using var source = await result.OpenReadAsync();
            await using var dest = File.Create(destPath);
            await source.CopyToAsync(dest);

            _settings.AvatarPath = "avatar.png";
            await _settings.SaveAsync();

            LoadAvatar();
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"Avatar change failed: {ex.Message}");
        }
    }

    private async void OnRemoveAvatarClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(_settings.AvatarPath))
            {
                var fullPath = Path.Combine(_settings.ProfileDirectory, _settings.AvatarPath);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }

            _settings.AvatarPath = null;
            await _settings.SaveAsync();

            LoadAvatar();
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"Avatar remove failed: {ex.Message}");
        }
    }

    private async void OnSaveUsernameClicked(object? sender, EventArgs e)
    {
        var newName = UsernameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            ShowUsernameError("Username cannot be empty");
            return;
        }

        if (newName == _auth.Username)
            return;

        UsernameErrorLabel.IsVisible = false;
        UsernameSuccessLabel.IsVisible = false;
        SaveUsernameBtn.IsEnabled = false;

        try
        {
            await _auth.ChangeUsernameAsync(newName);

            UsernameSuccessLabel.IsVisible = true;
            UsernameHintLabel.Text = $"Your handle: {_auth.Username}#{_auth.UserCode}";
            AvatarInitials.Text = newName.Length >= 2 ? newName[..2].ToUpperInvariant() : newName.ToUpperInvariant();
        }
        catch (Exception ex)
        {
            ShowUsernameError(ex.Message);
        }
        finally
        {
            SaveUsernameBtn.IsEnabled = true;
        }
    }

    private void ShowUsernameError(string msg)
    {
        UsernameErrorLabel.Text = msg;
        UsernameErrorLabel.IsVisible = true;
        UsernameSuccessLabel.IsVisible = false;
    }
}
