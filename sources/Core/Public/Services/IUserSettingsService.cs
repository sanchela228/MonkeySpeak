namespace Core.Public.Services;

public interface IUserSettingsService
{
    // Audio
    string? PreferredCaptureDeviceId { get; set; }
    string? PreferredPlaybackDeviceId { get; set; }
    float MasterVolume { get; set; }
    float MicrophoneVolume { get; set; }
    bool MicrophoneEnabled { get; set; }
    bool PlaybackEnabled { get; set; }
    bool NoiseSuppressionEnabled { get; set; }

    // UI
    int WindowWidth { get; set; }
    int WindowHeight { get; set; }
    bool SidebarCollapsed { get; set; }
    float InterfaceSoundVolume { get; set; }

    // Profile
    string? DisplayName { get; set; }
    string? AvatarPath { get; set; }

    // Events
    event EventHandler<string?>? AvatarChanged;

    // Persistence
    Task LoadAsync();
    Task SaveAsync();

    // Paths
    string ProfileDirectory { get; }
    string ProfileName { get; }
}
