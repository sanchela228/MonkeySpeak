using Core.Public.Services;

namespace Core.Public;

public class MonkeySpeakClient(
    IConnectionService connection,
    IAuthService auth,
    IFriendsService friends,
    ICallsService calls) : IMonkeySpeakClient
{
    public IConnectionService Connection { get; } = connection;
    public IAuthService Auth { get; } = auth;
    public IFriendsService Friends { get; } = friends;
    public ICallsService Calls { get; } = calls;
}