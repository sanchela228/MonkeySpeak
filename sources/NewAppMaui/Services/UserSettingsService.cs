using System.Text.Json;
using Core.Public.Services;

namespace NewAppMaui.Services;

public sealed class UserSettingsService : IUserSettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _profileDir;
    private readonly string _settingsPath;
    private SettingsData _data = new();
    private bool _dirty;

    public string ProfileDirectory => _profileDir;
    public string ProfileName { get; }

    public UserSettingsService()
    {
        var profile = Environment.GetEnvironmentVariable("MONKEYSPEAK_PROFILE")?.Trim();
        if (string.IsNullOrEmpty(profile)) profile = "default";
        ProfileName = profile;

        var baseDir = Path.Combine(FileSystem.AppDataDirectory, "MonkeySpeak");
        _profileDir = Path.Combine(baseDir, profile);
        _settingsPath = Path.Combine(_profileDir, "settings.json");

        Directory.CreateDirectory(_profileDir);
    }

    public event EventHandler<string?>? AvatarChanged;

    // Audio
    public string? PreferredCaptureDeviceId
    {
        get => _data.PreferredCaptureDeviceId;
        set { _data.PreferredCaptureDeviceId = value; _dirty = true; }
    }

    public string? PreferredPlaybackDeviceId
    {
        get => _data.PreferredPlaybackDeviceId;
        set { _data.PreferredPlaybackDeviceId = value; _dirty = true; }
    }

    public float MasterVolume
    {
        get => _data.MasterVolume;
        set { _data.MasterVolume = Math.Clamp(value, 0f, 1f); _dirty = true; }
    }

    public float MicrophoneVolume
    {
        get => _data.MicrophoneVolume;
        set { _data.MicrophoneVolume = Math.Clamp(value, 0f, 1f); _dirty = true; }
    }

    public bool MicrophoneEnabled
    {
        get => _data.MicrophoneEnabled;
        set { _data.MicrophoneEnabled = value; _dirty = true; }
    }

    public bool PlaybackEnabled
    {
        get => _data.PlaybackEnabled;
        set { _data.PlaybackEnabled = value; _dirty = true; }
    }

    // UI
    public int WindowWidth
    {
        get => _data.WindowWidth;
        set { _data.WindowWidth = value; _dirty = true; }
    }

    public int WindowHeight
    {
        get => _data.WindowHeight;
        set { _data.WindowHeight = value; _dirty = true; }
    }

    public bool SidebarCollapsed
    {
        get => _data.SidebarCollapsed;
        set { _data.SidebarCollapsed = value; _dirty = true; }
    }

    // Profile
    public string? DisplayName
    {
        get => _data.DisplayName;
        set { _data.DisplayName = value; _dirty = true; }
    }

    public string? AvatarPath
    {
        get => _data.AvatarPath;
        set { _data.AvatarPath = value; _dirty = true; AvatarChanged?.Invoke(this, value); }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var json = await File.ReadAllTextAsync(_settingsPath);
            var data = JsonSerializer.Deserialize<SettingsData>(json, JsonOpts);
            if (data is not null)
                _data = data;
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"Failed to load settings: {ex.Message}");
        }

        _dirty = false;
    }

    public async Task SaveAsync()
    {
        if (!_dirty) return;

        try
        {
            Directory.CreateDirectory(_profileDir);
            var json = JsonSerializer.Serialize(_data, JsonOpts);
            await File.WriteAllTextAsync(_settingsPath, json);
            _dirty = false;
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"Failed to save settings: {ex.Message}");
        }
    }

    private sealed class SettingsData
    {
        public string? PreferredCaptureDeviceId { get; set; }
        public string? PreferredPlaybackDeviceId { get; set; }
        public float MasterVolume { get; set; } = 1.0f;
        public float MicrophoneVolume { get; set; } = 1.0f;
        public bool MicrophoneEnabled { get; set; } = true;
        public bool PlaybackEnabled { get; set; } = true;
        public int WindowWidth { get; set; } = 800;
        public int WindowHeight { get; set; } = 600;
        public bool SidebarCollapsed { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarPath { get; set; }
    }
}
