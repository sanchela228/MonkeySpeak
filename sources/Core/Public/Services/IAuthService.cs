namespace Core.Public.Services;

public interface IAuthService
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? Username { get; }
    string? UserCode { get; }

    event EventHandler? Authenticated;
    event EventHandler<string>? AuthFailed;
    event EventHandler? LoggedOut;

    Task LoginAsync(string username, string password, CancellationToken ct = default);

    Task RegisterIfNeededAsync(CancellationToken ct = default);
    Task AuthenticateAsync(CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
}
