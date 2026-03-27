using Core.Public.Services;

namespace Core.Public;

public interface IMonkeySpeakClient
{
    IConnectionService Connection { get; }
    IAuthService Auth { get; }
    IFriendsService Friends { get; }
    ICallsService Calls { get; }
}