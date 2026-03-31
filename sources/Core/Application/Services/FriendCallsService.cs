using System.Text.Json;
using Core.Public.Services;
using Core.Websockets;
using Core.Websockets.Messages.AuthCall;

namespace Core.Application.Services;

public sealed class FriendCallsService : IFriendCallsService
{
    private readonly IConnectionService _connection;
    private readonly IAuthService _auth;

    private readonly object _sync = new();
    private TaskCompletionSource<bool>? _inviteTcs;
    private string _inviteRoomCode = string.Empty;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public event EventHandler<IncomingCallInvite>? IncomingCall;
    public event EventHandler<CallInviteResponseInfo>? InviteResponse;
    public event EventHandler<CallInviteCancelledInfo>? InviteCancelled;

    public FriendCallsService(IConnectionService connection, IAuthService auth)
    {
        _connection = connection;
        _auth = auth;
        _connection.MessageReceived += OnMessageReceived;
    }

    public async Task SendInviteAsync(string friendId, string roomCode, CancellationToken ct = default)
    {
        if (!_auth.IsAuthenticated)
            throw new InvalidOperationException("Not authenticated");

        if (string.IsNullOrWhiteSpace(friendId))
            throw new ArgumentException("FriendId is empty", nameof(friendId));

        if (string.IsNullOrWhiteSpace(roomCode))
            throw new ArgumentException("RoomCode is empty", nameof(roomCode));

        TaskCompletionSource<bool> tcs;
        lock (_sync)
        {
            if (_inviteTcs is not null)
                throw new InvalidOperationException("Another invite is already in progress");

            _inviteRoomCode = roomCode;
            _inviteTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs = _inviteTcs;
        }

        try
        {
            var msg = new InviteToCall
            {
                FriendId = friendId,
                RoomCode = roomCode,
                Value = "InviteToCall"
            };

            await SendAsync(msg, ct).ConfigureAwait(false);
            using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
            await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                _inviteTcs = null;
                _inviteRoomCode = string.Empty;
            }
        }
    }

    public async Task RespondAsync(string toUserId, string roomCode, bool accepted, string reason = "", CancellationToken ct = default)
    {
        if (!_auth.IsAuthenticated)
            throw new InvalidOperationException("Not authenticated");

        if (string.IsNullOrWhiteSpace(toUserId))
            throw new ArgumentException("ToUserId is empty", nameof(toUserId));

        if (string.IsNullOrWhiteSpace(roomCode))
            throw new ArgumentException("RoomCode is empty", nameof(roomCode));

        var msg = new CallInviteResponse
        {
            RoomCode = roomCode,
            ToUserId = toUserId,
            FromUserId = _auth.UserId ?? string.Empty,
            FromUsername = _auth.Username ?? string.Empty,
            Accepted = accepted,
            Reason = reason ?? string.Empty,
            Value = accepted ? "CallInviteAccepted" : "CallInviteRejected"
        };

        await SendAsync(msg, ct).ConfigureAwait(false);
    }

    public async Task CancelAsync(string friendId, string roomCode, CancellationToken ct = default)
    {
        if (!_auth.IsAuthenticated)
            throw new InvalidOperationException("Not authenticated");

        if (string.IsNullOrWhiteSpace(friendId))
            throw new ArgumentException("FriendId is empty", nameof(friendId));

        if (string.IsNullOrWhiteSpace(roomCode))
            throw new ArgumentException("RoomCode is empty", nameof(roomCode));

        var msg = new CancelCallInvite
        {
            FriendId = friendId,
            RoomCode = roomCode,
            Value = "CancelCallInvite"
        };

        await SendAsync(msg, ct).ConfigureAwait(false);
    }

    private void OnMessageReceived(object? sender, string raw)
    {
        try
        {
            var ctx = JsonSerializer.Deserialize<Context>(raw, JsonOpts);
            if (ctx is null) return;

            switch (ctx.Type)
            {
                case "Messages.AuthCall.CallInviteSent":
                {
                    var msg = ctx.Message.Deserialize<CallInviteSent>(JsonOpts);
                    if (msg is null) return;

                    TaskCompletionSource<bool>? tcs;
                    lock (_sync)
                    {
                        tcs = _inviteTcs;
                        if (tcs is null) break;
                        if (!string.Equals(_inviteRoomCode, msg.RoomCode, StringComparison.Ordinal)) break;
                    }

                    tcs.TrySetResult(true);
                    break;
                }

                case "Messages.AuthCall.IncomingCall":
                {
                    var msg = ctx.Message.Deserialize<IncomingCall>(JsonOpts);
                    if (msg is null) return;

                    IncomingCall?.Invoke(this, new IncomingCallInvite(
                        msg.RoomCode,
                        msg.FromUserId,
                        msg.FromUsername));
                    break;
                }

                case "Messages.AuthCall.CallInviteResponse":
                {
                    var msg = ctx.Message.Deserialize<CallInviteResponse>(JsonOpts);
                    if (msg is null) return;

                    InviteResponse?.Invoke(this, new CallInviteResponseInfo(
                        msg.RoomCode,
                        msg.Accepted,
                        msg.FromUserId,
                        msg.FromUsername,
                        msg.Reason));
                    break;
                }

                case "Messages.AuthCall.CallInviteCancelled":
                {
                    var msg = ctx.Message.Deserialize<CallInviteCancelled>(JsonOpts);
                    if (msg is null) return;

                    InviteCancelled?.Invoke(this, new CallInviteCancelledInfo(
                        msg.RoomCode,
                        msg.FromUserId,
                        msg.FromUsername));
                    break;
                }

                case "Messages.AuthCall.ErrorRegistration":
                {
                    var msg = ctx.Message.Deserialize<ErrorRegistration>(JsonOpts);
                    if (msg is null) return;

                    TaskCompletionSource<bool>? tcs;
                    lock (_sync) tcs = _inviteTcs;
                    if (tcs is null) return;

                    var error = $"{msg.ErrorCode}: {msg.Value}";
                    tcs.TrySetException(new InvalidOperationException(error));
                    break;
                }
            }
        }
        catch
        {
        }
    }

    private async Task SendAsync(IMessage message, CancellationToken ct)
    {
        var ctx = Context.Create(message);
        var json = JsonSerializer.Serialize(ctx);
        await _connection.SendAsync(json, ct).ConfigureAwait(false);
    }
}
