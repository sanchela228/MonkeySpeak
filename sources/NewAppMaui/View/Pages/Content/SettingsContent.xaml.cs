using Core.Domain.Media;
using Core.Public.Configurations;
using Core.Public.Services;
using NewAppMaui.Services;

namespace NewAppMaui.View.Pages.Content;

public partial class SettingsContent : ContentView
{
    private readonly IAuthService _auth;
    private readonly IAudioService _audio;
    private readonly IUserSettingsService _settings;
    private readonly IConnectionSettingsStore _connectionStore;
    private readonly AvatarCacheService _avatarCache;
    private bool _suppressPickerEvents;

    public event Action? ChangeServerRequested;

    public SettingsContent()
    {
        var services = ((App)Application.Current!).Services;
        _auth = services.GetRequiredService<IAuthService>();
        _audio = services.GetRequiredService<IAudioService>();
        _settings = services.GetRequiredService<IUserSettingsService>();
        _connectionStore = services.GetRequiredService<IConnectionSettingsStore>();
        _avatarCache = services.GetRequiredService<AvatarCacheService>();

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
        LoadAudioDevices();
        LoadVolumes();
        LoadInterfaceSoundVolume();
        LoadNoiseSupp();
        _ = LoadServerInfo();
    }

    // ── Avatar ──────────────────────────────────────────────

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

    private void OnAvatarPointerEntered(object? sender, PointerEventArgs e)
    {
        AvatarOverlay.BackgroundColor = Color.FromArgb("#AA000000");
        AvatarOverlay.Opacity = 1;
    }

    private void OnAvatarPointerExited(object? sender, PointerEventArgs e)
    {
        AvatarOverlay.Opacity = 0;
        AvatarOverlay.BackgroundColor = Colors.Transparent;
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

            _avatarCache.InvalidateOwn();
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

            _avatarCache.InvalidateOwn();
            LoadAvatar();
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"Avatar remove failed: {ex.Message}");
        }
    }

    // ── Username ─────────────────────────────────────────────

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

    // ── Audio devices ────────────────────────────────────────

    private void LoadAudioDevices()
    {
        try
        {
            if (!_audio.IsInitialized)
                _audio.Initialize();

            _audio.RefreshDevices();

            _suppressPickerEvents = true;

            var capList = _audio.CaptureDevices?.ToList() ?? [];
            var pbList = _audio.PlaybackDevices?.ToList() ?? [];

            CapturePicker.ItemsSource = capList;
            PlaybackPicker.ItemsSource = pbList;

            var prefCap = _settings.PreferredCaptureDeviceId;
            var prefPb = _settings.PreferredPlaybackDeviceId;

            CapturePicker.SelectedItem =
                (prefCap is not null ? capList.FirstOrDefault(d => d.Id == prefCap) : null)
                ?? capList.FirstOrDefault(d => d.IsDefault)
                ?? capList.FirstOrDefault();

            PlaybackPicker.SelectedItem =
                (prefPb is not null ? pbList.FirstOrDefault(d => d.Id == prefPb) : null)
                ?? pbList.FirstOrDefault(d => d.IsDefault)
                ?? pbList.FirstOrDefault();

            _suppressPickerEvents = false;
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"LoadAudioDevices failed: {ex.Message}");
            _suppressPickerEvents = false;
        }
    }

    private void OnCapturePickerChanged(object? sender, EventArgs e)
    {
        if (_suppressPickerEvents) return;
        if (CapturePicker.SelectedItem is not AudioDeviceInfo dev) return;

        try
        {
            _audio.SwitchCaptureDevice(dev.Id);
            _settings.PreferredCaptureDeviceId = dev.Id;
            _ = _settings.SaveAsync();
        }
        catch (Exception ex) { Core.Logger.Warn($"SwitchCapture failed: {ex.Message}"); }
    }

    private void OnPlaybackPickerChanged(object? sender, EventArgs e)
    {
        if (_suppressPickerEvents) return;
        if (PlaybackPicker.SelectedItem is not AudioDeviceInfo dev) return;

        try
        {
            _audio.SwitchPlaybackDevice(dev.Id);
            _settings.PreferredPlaybackDeviceId = dev.Id;
            _ = _settings.SaveAsync();
        }
        catch (Exception ex) { Core.Logger.Warn($"SwitchPlayback failed: {ex.Message}"); }
    }

    // ── Volume ───────────────────────────────────────────────

    private void LoadVolumes()
    {
        MicVolumeSlider.Value = _audio.MicrophoneVolume;
        PlaybackVolumeSlider.Value = _audio.PlaybackVolume;
        MicVolumeLabel.Text = $"{_audio.MicrophoneVolume}%";
        PlaybackVolumeLabel.Text = $"{_audio.PlaybackVolume}%";
    }

    private void OnMicVolumeChanged(object? sender, ValueChangedEventArgs e)
    {
        var vol = (int)e.NewValue;
        _audio.MicrophoneVolume = vol;
        MicVolumeLabel.Text = $"{vol}%";
        _settings.MicrophoneVolume = vol / 100f;
        _ = _settings.SaveAsync();
    }

    private void OnPlaybackVolumeChanged(object? sender, ValueChangedEventArgs e)
    {
        var vol = (int)e.NewValue;
        _audio.PlaybackVolume = vol;
        PlaybackVolumeLabel.Text = $"{vol}%";
        _settings.MasterVolume = vol / 100f;
        _ = _settings.SaveAsync();
    }

    private void LoadInterfaceSoundVolume()
    {
        var pct = (int)(_settings.InterfaceSoundVolume * 100);
        InterfaceSoundVolumeSlider.Value = pct;
        InterfaceSoundVolumeLabel.Text = $"{pct}%";
    }

    private void OnInterfaceSoundVolumeChanged(object? sender, ValueChangedEventArgs e)
    {
        var vol = (int)e.NewValue;
        InterfaceSoundVolumeLabel.Text = $"{vol}%";
        _settings.InterfaceSoundVolume = vol / 100f;
        _ = _settings.SaveAsync();
    }

    // ── Noise suppression ────────────────────────────────────

    private void LoadNoiseSupp()
    {
        NoiseSuppSwitch.IsToggled = _audio.IsNoiseSuppressionEnabled;
    }

    private void OnNoiseSuppressionToggled(object? sender, ToggledEventArgs e)
    {
        _audio.IsNoiseSuppressionEnabled = e.Value;
        _settings.NoiseSuppressionEnabled = e.Value;
        _ = _settings.SaveAsync();
    }

    // ── Server ───────────────────────────────────────────────

    private async Task LoadServerInfo()
    {
        var profile = await _connectionStore.GetActiveAsync();
        if (profile is not null)
        {
            ServerNameLabel.Text = profile.Name;
            ServerAddressLabel.Text = profile.ConnectionString;
        }
    }

    private async void OnHandleLabelTapped(object? sender, TappedEventArgs e)
    {
        if (_auth.Username is null || _auth.UserCode is null) return;

        var handle = $"{_auth.Username}#{_auth.UserCode}";
        await Clipboard.Default.SetTextAsync(handle);

        if (sender is Label label)
        {
            var original = label.Text;
            label.Text = "Copied!";
            await Task.Delay(1500);
            label.Text = original;
        }
    }

    private void OnChangeServerClicked(object? sender, EventArgs e)
    {
        if (Window is not null)
            Window.Page = ((App)Application.Current!).Services
                .GetRequiredService<ConnectionProfileSelectPage>();
    }
}
