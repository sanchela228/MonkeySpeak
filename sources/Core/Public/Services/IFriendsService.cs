using Core.Domain.Friends;

namespace Core.Public.Services;

public interface IFriendsService
{
    IReadOnlyList<Friend> Friends { get; }
    IReadOnlyList<FriendRequest> Pending { get; }

    event EventHandler? FriendsUpdated;
    event EventHandler? PendingUpdated;

    Task RefreshAsync(CancellationToken ct = default);

    Task SendFriendRequestAsync(string friendUsername, CancellationToken ct = default);
    Task AcceptAsync(string friendshipId, CancellationToken ct = default);
    Task RejectAsync(string friendshipId, CancellationToken ct = default);
    Task RemoveFriendAsync(string friendId, CancellationToken ct = default);
}