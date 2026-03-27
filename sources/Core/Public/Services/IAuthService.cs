namespace Core.Public.Services;

public interface IAuthService
{
    bool IsAuthenticated { get; }
    event EventHandler? Authenticated;
    event EventHandler<string>? AuthFailed;

    Task RegisterIfNeededAsync(CancellationToken ct = default);
    Task AuthenticateAsync(CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
}