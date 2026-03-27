using Core.Domain.Friends;
using Core.Public.Services;

namespace Core.Application.Services;

public class FriendsService : IFriendsService
{
    public IReadOnlyList<Friend> Friends { get; }
    public IReadOnlyList<FriendRequest> Pending { get; }
    public event EventHandler? FriendsUpdated;
    public event EventHandler? PendingUpdated;
    public Task RefreshAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task SendFriendRequestAsync(string userId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task AcceptAsync(string userId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task RejectAsync(string userId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}