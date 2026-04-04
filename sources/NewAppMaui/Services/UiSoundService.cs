using Core.Public.Services;
using Plugin.Maui.Audio;

namespace NewAppMaui.Services;

public sealed class UiSoundService
{
    private readonly IAudioManager _audioManager;
    private readonly IUserSettingsService _settings;

    public UiSoundService(IAudioManager audioManager, IUserSettingsService settings)
    {
        _audioManager = audioManager;
        _settings = settings;
    }

    public void PlayConnect() => Play("Sounds/Room/connect.wav");
    public void PlayDisconnect() => Play("Sounds/Room/disconnect.wav");
    public void PlayMicOn() => Play("Sounds/Room/miconv2.wav");
    public void PlayMicOff() => Play("Sounds/Room/micoffv2.wav");

    private void Play(string resource)
    {
        var volume = _settings.InterfaceSoundVolume;
        if (volume <= 0f) return;

        _ = PlayAsync(resource, volume);
    }

    private async Task PlayAsync(string resource, float volume)
    {
        IAudioPlayer? player = null;
        try
        {
            var stream = await FileSystem.OpenAppPackageFileAsync(resource);
            player = _audioManager.CreatePlayer(stream);
            player.Volume = volume;
            player.PlaybackEnded += OnPlaybackEnded;
            player.Play();
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"UiSoundService.Play({resource}) failed: {ex.Message}");
            player?.Dispose();
        }
    }

    private static void OnPlaybackEnded(object? sender, EventArgs e)
    {
        if (sender is not IAudioPlayer player) return;

        player.PlaybackEnded -= OnPlaybackEnded;
        player.Dispose();
    }
}
