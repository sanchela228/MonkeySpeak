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

    public event EventHandler? FriendsUpdated;
    public event EventHandler? PendingUpdated;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        EnsureWsConnected();

        await SendAsync(new GetFriendList { Value = "Get friend list" }, ct).ConfigureAwait(false);
        await SendAsync(new GetPendingFriendList { Value = "Get pending friend list" }, ct).ConfigureAwait(false);
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
                _pendingActionsInFlight.Clear();
            }

            FriendsUpdated?.Invoke(this, EventArgs.Empty);
            PendingUpdated?.Invoke(this, EventArgs.Empty);
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
                case "Messages.AuthCall.FriendRequestReceived":
                    HandleFriendRequestReceived(ctx);
                    break;
                case "Messages.AuthCall.FriendAccepted":
                    HandleFriendAccepted(ctx);
                    break;
                case "Messages.AuthCall.FriendRequestRejected":
                    HandleFriendRequestRejected(ctx);
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

        var updated = false;
        lock (_sync)
        {
            updated = _pending.RemoveAll(x => x.FriendshipId == (msg.FriendshipId ?? string.Empty)) > 0;
        }

        if (updated)
            PendingUpdated?.Invoke(this, EventArgs.Empty);
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
}