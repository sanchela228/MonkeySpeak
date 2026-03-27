using Core.Domain.Friends;

namespace Core.Public.Services;

public interface IFriendsService
{
    IReadOnlyList<Friend> Friends { get; }
    IReadOnlyList<FriendRequest> Pending { get; }

    event EventHandler? FriendsUpdated;
    event EventHandler? PendingUpdated;

    Task RefreshAsync(CancellationToken ct = default);

    Task SendFriendRequestAsync(string userId, CancellationToken ct = default);
    Task AcceptAsync(string userId, CancellationToken ct = default);
    Task RejectAsync(string userId, CancellationToken ct = default);
}