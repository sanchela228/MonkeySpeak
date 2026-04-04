using Core.Public.Services;
using NewAppMaui.Services;
using NewAppMaui.View.Layout;

namespace NewAppMaui;

public sealed class FriendCallsUiCoordinator
{
    private readonly IFriendCallsService _friendCalls;
    private readonly ICallsService _calls;
    private readonly CallUiController _callController;
    private readonly AvatarCacheService _avatarCache;

    private MainLayout? _layout;
    private IncomingCallInvite? _activeInvite;
    private string? _currentFriendUserId;

    public FriendCallsUiCoordinator(
        IFriendCallsService friendCalls,
        ICallsService calls,
        CallUiController callController,
        AvatarCacheService avatarCache)
    {
        _friendCalls = friendCalls;
        _calls = calls;
        _callController = callController;
        _avatarCache = avatarCache;

        _friendCalls.IncomingCall += OnIncomingCall;
        _friendCalls.InviteCancelled += OnInviteCancelled;
        _calls.AvatarReceived += OnAvatarReceived;
        _callController.CallEnded += () => _currentFriendUserId = null;
    }

    public void AttachLayout(MainLayout layout)
    {
        _layout = layout;

        layout.IncomingCallAcceptRequested += async () => await AcceptIncomingCallAsync();
        layout.IncomingCallRejectRequested += async () => await RejectIncomingCallAsync();
    }

    private async Task AcceptIncomingCallAsync()
    {
        var invite = _activeInvite;
        if (invite is null) return;

        _activeInvite = null;
        _layout?.HideIncomingCall();

        try
        {
            if (_callController.IsInCall)
                await _callController.EndCallAsync("NewCall");

            _currentFriendUserId = invite.FromUserId;
            await _friendCalls.RespondAsync(invite.FromUserId, invite.RoomCode, accepted: true);

            var session = await _calls.JoinAsync(invite.RoomCode);

            var initial = session.Interlocutors
                .Select(x => new Core.Websockets.Messages.NoAuthCall.InterlocutorJoined
                {
                    Id = x.Id,
                    IpEndPoint = x.RemoteIp.ToString()
                })
                .ToArray();

            _callController.StartCall(session.RoomCode, initial);
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"AcceptIncomingCall failed: {ex.Message}");
        }
    }

    private async Task RejectIncomingCallAsync()
    {
        var invite = _activeInvite;
        if (invite is null) return;

        _activeInvite = null;
        _layout?.HideIncomingCall();

        try
        {
            await _friendCalls.RespondAsync(invite.FromUserId, invite.RoomCode, accepted: false, reason: "Declined");
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"RejectIncomingCall failed: {ex.Message}");
        }
    }

    public void SetCurrentFriendUserId(string? userId)
    {
        _currentFriendUserId = userId;
    }

    private void OnAvatarReceived(string interlocutorId, byte[] data)
    {
        if (_currentFriendUserId is not null)
            _avatarCache.SaveAvatarForUser(_currentFriendUserId, data);
    }

    private void OnIncomingCall(object? sender, IncomingCallInvite invite)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_callController.IsInCall)
            {
                try { await _friendCalls.RespondAsync(invite.FromUserId, invite.RoomCode, accepted: false, reason: "Busy"); }
                catch { }
                return;
            }

            if (_activeInvite is not null)
            {
                try { await _friendCalls.RespondAsync(invite.FromUserId, invite.RoomCode, accepted: false, reason: "Busy"); }
                catch { }
                return;
            }

            _activeInvite = invite;
            _layout?.ShowIncomingCall(invite.FromUsername);
        });
    }

    private void OnInviteCancelled(object? sender, CallInviteCancelledInfo info)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_activeInvite is not null &&
                string.Equals(_activeInvite.RoomCode, info.RoomCode, StringComparison.OrdinalIgnoreCase))
            {
                _activeInvite = null;
                _layout?.HideIncomingCall();
            }
        });
    }
}
