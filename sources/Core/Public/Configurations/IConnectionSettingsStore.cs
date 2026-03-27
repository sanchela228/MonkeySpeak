namespace Core.Public.Configurations;

public interface IConnectionSettingsStore
{
    bool HasExplicitActiveProfileSelection { get; }

    Task<IReadOnlyList<ConnectionProfile>> GetProfilesAsync(CancellationToken ct = default);
    Task<ConnectionProfile?> GetActiveAsync(CancellationToken ct = default);
    Task SetActiveAsync(string profileId, CancellationToken ct = default);

    Task UpsertUserProfileAsync(ConnectionProfile profile, CancellationToken ct = default);
    Task DeleteUserProfileAsync(string profileId, CancellationToken ct = default);

    event EventHandler? Changed;
}