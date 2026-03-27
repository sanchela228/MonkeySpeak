using Core.Public.Services;

namespace Core.Application.Services;

public class AuthService : IAuthService
{
    public bool IsAuthenticated { get; }
    public event EventHandler? Authenticated;
    public event EventHandler<string>? AuthFailed;
    public Task RegisterIfNeededAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task AuthenticateAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task LogoutAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}