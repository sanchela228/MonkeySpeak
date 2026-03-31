namespace Core.Public.Services;

public record IncomingCallInvite(string RoomCode, string FromUserId, string FromUsername);

public record CallInviteResponseInfo(string RoomCode, bool Accepted, string FromUserId, string FromUsername, string Reason);

public record CallInviteCancelledInfo(string RoomCode, string FromUserId, string FromUsername);

public interface IFriendCallsService
{
    event EventHandler<IncomingCallInvite>? IncomingCall;
    event EventHandler<CallInviteResponseInfo>? InviteResponse;
    event EventHandler<CallInviteCancelledInfo>? InviteCancelled;

    Task SendInviteAsync(string friendId, string roomCode, CancellationToken ct = default);
    Task RespondAsync(string toUserId, string roomCode, bool accepted, string reason = "", CancellationToken ct = default);
    Task CancelAsync(string friendId, string roomCode, CancellationToken ct = default);
}
