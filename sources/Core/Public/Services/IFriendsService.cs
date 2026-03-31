using Core.Domain.Friends;

namespace Core.Public.Services;

public interface IFriendsService
{
    IReadOnlyList<Friend> Friends { get; }
    IReadOnlyList<FriendRequest> Pending { get; }
    IReadOnlyList<OutgoingFriendRequest> PendingSent { get; }

    event EventHandler? FriendsUpdated;
    event EventHandler? PendingUpdated;
    event EventHandler? PendingSentUpdated;

    Task RefreshAsync(CancellationToken ct = default);

    Task SendFriendRequestAsync(string friendUsername, CancellationToken ct = default);
    Task AcceptAsync(string friendshipId, CancellationToken ct = default);
    Task RejectAsync(string friendshipId, CancellationToken ct = default);
    Task RemoveFriendAsync(string friendId, CancellationToken ct = default);
    Task CancelPendingAsync(string friendshipId, CancellationToken ct = default);
}