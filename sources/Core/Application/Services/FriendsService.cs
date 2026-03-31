using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using Core.Domain.Friends;
using Core.Public.Services;
using Core.Websockets;
using Core.Websockets.Messages.AuthCall;

namespace Core.Application.Services;

public class FriendsService : IFriendsService
{
    private readonly IConnectionService _connection;
    private readonly object _sync = new();
    private readonly HashSet<string> _pendingActionsInFlight = new();

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private List<Friend> _friends = new();
    private List<FriendRequest> _pending = new();
    private List<OutgoingFriendRequest> _pendingSent = new();

    public FriendsService(IConnectionService connection)
    {
        _connection = connection;
        _connection.MessageReceived += OnMessageReceived;
        _connection.StateChanged += OnConnectionStateChanged;
    }

    public IReadOnlyList<Friend> Friends
    {
        get
        {
            lock (_sync)
                return _friends;
        }
    }

    public IReadOnlyList<FriendRequest> Pending
    {
        get
        {
            lock (_sync)
                return _pending;
        }
    }

    public IReadOnlyList<OutgoingFriendRequest> PendingSent
    {
        get
        {
            lock (_sync)
                return _pendingSent;
        }
    }

    public event EventHandler? FriendsUpdated;
    public event EventHandler? PendingUpdated;
    public event EventHandler? PendingSentUpdated;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        EnsureWsConnected();

        await SendAsync(new GetFriendList { Value = "Get friend list" }, ct).ConfigureAwait(false);
        await SendAsync(new GetPendingFriendList { Value = "Get pending friend list" }, ct).ConfigureAwait(false);
        await SendAsync(new GetOutgoingPendingFriendList { Value = "Get outgoing pending friend list" }, ct).ConfigureAwait(false);
    }

    public Task SendFriendRequestAsync(string friendUsername, CancellationToken ct = default)
    {
        EnsureWsConnected();
        if (string.IsNullOrWhiteSpace(friendUsername))
            return Task.CompletedTask;

        return SendAsync(new AddFriend
        {
            FriendUsername = friendUsername.Trim(),
            Value = "Add friend"
        }, ct);
    }

    public async Task CancelPendingAsync(string friendshipId, CancellationToken ct = default)
    {
        EnsureWsConnected();
        if (string.IsNullOrWhiteSpace(friendshipId))
            return;

        if (!BeginPendingAction(friendshipId))
            return;

        try
        {
            RemovePendingSentLocal(friendshipId);
            await SendAsync(new CancelFriendRequest { FriendshipId = friendshipId, Value = "Cancel friend request" }, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            EndPendingAction(friendshipId);
        }
    }

    public async Task AcceptAsync(string friendshipId, CancellationToken ct = default)
    {
        EnsureWsConnected();
        if (string.IsNullOrWhiteSpace(friendshipId))
            return;

        if (!BeginPendingAction(friendshipId))
            return;

        try
        {
            RemovePendingLocal(friendshipId);
            await SendAsync(new AcceptFriend { FriendshipId = friendshipId, Value = "Accept friend" }, ct).ConfigureAwait(false);
        }
        finally
        {
            EndPendingAction(friendshipId);
        }
    }

    public async Task RejectAsync(string friendshipId, CancellationToken ct = default)
    {
        EnsureWsConnected();
        if (string.IsNullOrWhiteSpace(friendshipId))
            return;

        if (!BeginPendingAction(friendshipId))
            return;

        try
        {
            RemovePendingLocal(friendshipId);
            await SendAsync(new RejectFriend { FriendshipId = friendshipId, Value = "Reject friend" }, ct).ConfigureAwait(false);
        }
        finally
        {
            EndPendingAction(friendshipId);
        }
    }

    public Task RemoveFriendAsync(string friendId, CancellationToken ct = default)
    {
        EnsureWsConnected();
        if (string.IsNullOrWhiteSpace(friendId))
            return Task.CompletedTask;

        return SendAsync(new RemoveFriend { FriendId = friendId, Value = "Remove friend" }, ct);
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        if (state is ConnectionState.Closed or ConnectionState.Broken)
        {
            lock (_sync)
            {
                _friends = new List<Friend>();
                _pending = new List<FriendRequest>();
                _pendingSent = new List<OutgoingFriendRequest>();
                _pendingActionsInFlight.Clear();
            }

            FriendsUpdated?.Invoke(this, EventArgs.Empty);
            PendingUpdated?.Invoke(this, EventArgs.Empty);
            PendingSentUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMessageReceived(object? sender, string raw)
    {
        try
        {
            var ctx = JsonSerializer.Deserialize<Context>(raw, JsonOpts);
            if (ctx is null) return;

            switch (ctx.Type)
            {
                case "Messages.AuthCall.FriendListResponse":
                    HandleFriendList(ctx);
                    break;
                case "Messages.AuthCall.PendingFriendListResponse":
                    HandlePendingList(ctx);
                    break;
                case "Messages.AuthCall.OutgoingPendingFriendListResponse":
                    HandleOutgoingPendingList(ctx);
                    break;
                case "Messages.AuthCall.FriendRequestReceived":
                    HandleFriendRequestReceived(ctx);
                    break;
                case "Messages.AuthCall.FriendRequestSent":
                    HandleFriendRequestSent(ctx);
                    break;
                case "Messages.AuthCall.FriendAccepted":
                    HandleFriendAccepted(ctx);
                    break;
                case "Messages.AuthCall.FriendRequestRejected":
                    HandleFriendRequestRejected(ctx);
                    break;
                case "Messages.AuthCall.FriendRequestCancelled":
                    HandleFriendRequestCancelled(ctx);
                    break;
                case "Messages.AuthCall.FriendOnline":
                    HandleFriendOnline(ctx);
                    break;
                case "Messages.AuthCall.FriendOffline":
                    HandleFriendOffline(ctx);
                    break;
                case "Messages.AuthCall.FriendRemoved":
                    HandleFriendRemoved(ctx);
                    break;
            }
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"FriendsService message parse error: {ex.Message}");
        }
    }

    private void HandleFriendList(Context ctx)
    {
        var msg = ctx.Message.Deserialize<FriendListResponse>(JsonOpts);
        if (msg is null) return;

        lock (_sync)
        {
            _friends = msg.Friends.Select(f => new Friend
            {
                UserId = f.UserId ?? string.Empty,
                Username = f.Username ?? string.Empty,
                IsOnline = f.IsOnline,
                LastSeenAt = f.LastSeenAt ?? string.Empty
            }).ToList();
        }

        FriendsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void HandlePendingList(Context ctx)
    {
        var msg = ctx.Message.Deserialize<PendingFriendListResponse>(JsonOpts);
        if (msg is null) return;

        lock (_sync)
        {
            _pending = msg.Friends.Select(r => new FriendRequest
            {
                FriendshipId = r.FriendshipId ?? string.Empty,
                FromUserId = r.FromUserId ?? string.Empty,
                FromUsername = r.FromUsername ?? string.Empty
            }).ToList();
        }

        PendingUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void HandleOutgoingPendingList(Context ctx)
    {
        var msg = ctx.Message.Deserialize<OutgoingPendingFriendListResponse>(JsonOpts);
        if (msg is null) return;

        lock (_sync)
        {
            _pendingSent = msg.Friends.Select(r => new OutgoingFriendRequest
            {
                FriendshipId = r.FriendshipId ?? string.Empty,
                ToUserId = r.ToUserId ?? string.Empty,
                ToUsername = r.ToUsername ?? string.Empty
            }).ToList();
        }

        PendingSentUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void HandleFriendRequestSent(Context ctx)
    {
        var msg = ctx.Message.Deserialize<FriendRequestSent>(JsonOpts);
        if (msg is null) return;

        var req = new OutgoingFriendRequest
        {
            FriendshipId = msg.FriendshipId ?? string.Empty,
            ToUserId = msg.FriendId ?? string.Empty,
            ToUsername = msg.FriendUsername ?? string.Empty
        };

        var updated = false;
        lock (_sync)
        {
            if (!_pendingSent.Any(x => x.FriendshipId == req.FriendshipId))
            {
                _pendingSent.Add(req);
                updated = true;
            }
        }

        if (updated)
            PendingSentUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void HandleFriendRequestReceived(Context ctx)
    {
        var msg = ctx.Message.Deserialize<FriendRequestReceived>(JsonOpts);
        if (msg is null) return;

        var req = new FriendRequest
        {
            FriendshipId = msg.FriendshipId ?? string.Empty,
            FromUserId = msg.FromUserId ?? string.Empty,
            FromUsername = msg.FromUsername ?? string.Empty
        };

        var updated = false;
        lock (_sync)
        {
            if (!_pending.Any(x => x.FriendshipId == req.FriendshipId))
            {
                _pending.Add(req);
                updated = true;
            }
        }

        if (updated)
            PendingUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void HandleFriendRequestCancelled(Context ctx)
    {
        var msg = ctx.Message.Deserialize<FriendRequestCancelled>(JsonOpts);
        if (msg is null) return;

        var id = msg.FriendshipId ?? string.Empty;
        var updatedSent = false;
        var updatedIncoming = false;
        lock (_sync)
        {
            updatedSent = _pendingSent.RemoveAll(x => x.FriendshipId == id) > 0;
            updatedIncoming = _pending.RemoveAll(x => x.FriendshipId == id) > 0;
        }

        if (updatedSent)
            PendingSentUpdated?.Invoke(this, EventArgs.Empty);
        if (updatedIncoming)
            PendingUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void HandleFriendAccepted(Context ctx)
    {
        var msg = ctx.Message.Deserialize<FriendAccepted>(JsonOpts);
        if (msg is null) return;

        _ = RefreshAsync();
    }

    private void HandleFriendRequestRejected(Context ctx)
    {
        var msg = ctx.Message.Deserialize<FriendRequestRejected>(JsonOpts);
        if (msg is null) return;

        var id = msg.FriendshipId ?? string.Empty;
        var updatedIncoming = false;
        var updatedSent = false;
        lock (_sync)
        {
            updatedIncoming = _pending.RemoveAll(x => x.FriendshipId == id) > 0;
            updatedSent = _pendingSent.RemoveAll(x => x.FriendshipId == id) > 0;
        }

        if (updatedIncoming)
            PendingUpdated?.Invoke(this, EventArgs.Empty);
        if (updatedSent)
            PendingSentUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void HandleFriendOnline(Context ctx)
    {
        var msg = ctx.Message.Deserialize<FriendOnline>(JsonOpts);
        if (msg is null) return;

        UpdateOnlineState(msg.FriendId, online: true);
    }

    private void HandleFriendOffline(Context ctx)
    {
        var msg = ctx.Message.Deserialize<FriendOffline>(JsonOpts);
        if (msg is null) return;

        UpdateOnlineState(msg.FriendId, online: false);
    }

    private void HandleFriendRemoved(Context ctx)
    {
        var msg = ctx.Message.Deserialize<FriendRemoved>(JsonOpts);
        if (msg is null) return;

        var updated = false;
        lock (_sync)
        {
            updated = _friends.RemoveAll(x => x.UserId == (msg.FriendId ?? string.Empty)) > 0;
        }

        if (updated)
            FriendsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateOnlineState(string? friendId, bool online)
    {
        if (string.IsNullOrWhiteSpace(friendId))
            return;

        var updated = false;
        lock (_sync)
        {
            var f = _friends.FirstOrDefault(x => x.UserId == friendId);
            if (f is not null && f.IsOnline != online)
            {
                f.IsOnline = online;
                updated = true;
            }
        }

        if (updated)
            FriendsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureWsConnected()
    {
        if (_connection.State != ConnectionState.Open)
            throw new InvalidOperationException("WebSocket is not connected.");
    }

    private Task SendAsync(IMessage message, CancellationToken ct = default)
    {
        var ctx = Context.Create(message);
        var json = JsonSerializer.Serialize(ctx);
        return _connection.SendAsync(json, ct);
    }

    private bool BeginPendingAction(string id)
    {
        lock (_sync)
            return _pendingActionsInFlight.Add(id);
    }

    private void EndPendingAction(string id)
    {
        lock (_sync)
            _pendingActionsInFlight.Remove(id);
    }

    private void RemovePendingLocal(string friendshipId)
    {
        var updated = false;
        lock (_sync)
        {
            updated = _pending.RemoveAll(x => x.FriendshipId == friendshipId) > 0;
        }

        if (updated)
            PendingUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void RemovePendingSentLocal(string friendshipId)
    {
        var updated = false;
        lock (_sync)
        {
            updated = _pendingSent.RemoveAll(x => x.FriendshipId == friendshipId) > 0;
        }

        if (updated)
            PendingSentUpdated?.Invoke(this, EventArgs.Empty);
    }
}