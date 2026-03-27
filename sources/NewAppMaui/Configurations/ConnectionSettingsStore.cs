using System.Text.Json;
using Core.Public.Configurations;
using ConnectionProfile = Core.Public.Configurations.ConnectionProfile;

namespace NewAppMaui.Configurations;

public sealed class ConnectionSettingsStore : IConnectionSettingsStore
{
    private const string ActiveProfileIdKey = "connection.activeProfileId";
    private const string UserProfilesJsonKey = "connection.userProfiles.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyList<ConnectionProfile> _builtInProfiles =
    [
        new ConnectionProfile(
            Id: "default",
            Name: "Default",
            Domain: "95.163.230.160",
            Port: 80,
            UseSsl: false,
            MaxRetries: 2
        ),
        
        new ConnectionProfile(
            Id: "local",
            Name: "Localhost",
            Domain: "localhost",
            Port: 80,
            UseSsl: false,
            MaxRetries: 2
        )
    ];

    public bool HasExplicitActiveProfileSelection => Preferences.ContainsKey(ActiveProfileIdKey);

    public event EventHandler? Changed;

    public Task<IReadOnlyList<ConnectionProfile>> GetProfilesAsync(CancellationToken ct = default)
    {
        var user = LoadUserProfiles();
        var merged = new List<ConnectionProfile>(_builtInProfiles.Count + user.Count);

        merged.AddRange(_builtInProfiles);

        foreach (var p in user)
        {
            if (merged.All(x => !string.Equals(x.Id, p.Id, StringComparison.Ordinal)))
                merged.Add(p);
        }

        return Task.FromResult<IReadOnlyList<ConnectionProfile>>(merged);
    }

    Task<ConnectionProfile?> IConnectionSettingsStore.GetActiveAsync(CancellationToken ct)
    {
        return GetActiveAsync(ct);
    }

    Task<IReadOnlyList<ConnectionProfile>> IConnectionSettingsStore.GetProfilesAsync(CancellationToken ct)
    {
        return GetProfilesAsync(ct);
    }

    public async Task<ConnectionProfile?> GetActiveAsync(CancellationToken ct = default)
    {
        var profiles = await GetProfilesAsync(ct).ConfigureAwait(false);
        var activeId = Preferences.Get(ActiveProfileIdKey, _builtInProfiles[0].Id);

        return profiles.FirstOrDefault(p => string.Equals(p.Id, activeId, StringComparison.Ordinal))
               ?? profiles.FirstOrDefault();
    }

    public Task SetActiveAsync(string profileId, CancellationToken ct = default)
    {
        Preferences.Set(ActiveProfileIdKey, profileId);
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    Task IConnectionSettingsStore.UpsertUserProfileAsync(ConnectionProfile profile, CancellationToken ct)
    {
        return UpsertUserProfileAsync(profile, ct);
    }

    public Task UpsertUserProfileAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        var list = LoadUserProfiles();
        var idx = list.FindIndex(p => string.Equals(p.Id, profile.Id, StringComparison.Ordinal));
        if (idx >= 0)
            list[idx] = profile;
        else
            list.Add(profile);

        SaveUserProfiles(list);
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task DeleteUserProfileAsync(string profileId, CancellationToken ct = default)
    {
        var list = LoadUserProfiles();
        list.RemoveAll(p => string.Equals(p.Id, profileId, StringComparison.Ordinal));
        SaveUserProfiles(list);

        var activeId = Preferences.Get(ActiveProfileIdKey, _builtInProfiles[0].Id);
        if (string.Equals(activeId, profileId, StringComparison.Ordinal))
            Preferences.Set(ActiveProfileIdKey, _builtInProfiles[0].Id);

        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private static List<ConnectionProfile> LoadUserProfiles()
    {
        var json = Preferences.Get(UserProfilesJsonKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<ConnectionProfile>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveUserProfiles(List<ConnectionProfile> profiles)
    {
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        Preferences.Set(UserProfilesJsonKey, json);
    }
}
