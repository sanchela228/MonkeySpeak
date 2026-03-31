using Core.Public.Services;
using Microsoft.Extensions.DependencyInjection;

namespace NewAppMaui;

public sealed class FriendCallsUiCoordinator
{
    private readonly IFriendCallsService _friendCalls;

    private IncomingFriendCallPage? _activeIncoming;
    private string _activeRoomCode = string.Empty;

    public FriendCallsUiCoordinator(IFriendCallsService friendCalls)
    {
        _friendCalls = friendCalls;
        _friendCalls.IncomingCall += OnIncomingCall;
        _friendCalls.InviteCancelled += OnInviteCancelled;
    }

    private void OnIncomingCall(object? sender, IncomingCallInvite e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (Shell.Current is null)
                    return;

                // If already in a call, auto-reject.
                var calls = ((App)Application.Current!).Services.GetRequiredService<ICallsService>();
                if (calls.Current is not null)
                {
                    try
                    {
                        await _friendCalls.RespondAsync(e.FromUserId, e.RoomCode, accepted: false, reason: "Busy");
                    }
                    catch
                    {
                    }

                    return;
                }

                // If another incoming dialog is active, reject the new one.
                if (_activeIncoming is not null)
                {
                    try
                    {
                        await _friendCalls.RespondAsync(e.FromUserId, e.RoomCode, accepted: false, reason: "Busy");
                    }
                    catch
                    {
                    }

                    return;
                }

                var page = ((App)Application.Current!).Services.GetRequiredService<IncomingFriendCallPage>();
                page.Initialize(e);
                _activeIncoming = page;
                _activeRoomCode = page.RoomCode;

                page.Disappearing += (_, _) =>
                {
                    try
                    {
                        if (ReferenceEquals(_activeIncoming, page))
                        {
                            _activeIncoming = null;
                            _activeRoomCode = string.Empty;
                        }
                    }
                    catch
                    {
                    }
                };

                await Shell.Current.Navigation.PushModalAsync(new NavigationPage(page));
            }
            catch
            {
            }
            finally
            {
                // Note: page clears itself on navigation; here we just keep a best-effort reference.
            }
        });
    }

    private void OnInviteCancelled(object? sender, CallInviteCancelledInfo e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (_activeIncoming is not null
                    && !string.IsNullOrWhiteSpace(_activeRoomCode)
                    && string.Equals(_activeRoomCode, e.RoomCode, StringComparison.OrdinalIgnoreCase))
                {
                    _activeIncoming.MarkCancelledByCaller();
                    _activeIncoming = null;
                    _activeRoomCode = string.Empty;
                    return;
                }

                // Fallback: find active modal page by room code and close it.
                try
                {
                    var nav = Shell.Current?.Navigation;
                    if (nav is null) return;

                    var modal = nav.ModalStack.LastOrDefault();
                    if (modal is NavigationPage np && np.RootPage is IncomingFriendCallPage p)
                    {
                        if (string.Equals(p.RoomCode, e.RoomCode, StringComparison.OrdinalIgnoreCase))
                        {
                            p.MarkCancelledByCaller();
                            _activeIncoming = null;
                            _activeRoomCode = string.Empty;
                        }
                    }
                    else if (modal is IncomingFriendCallPage p2)
                    {
                        if (string.Equals(p2.RoomCode, e.RoomCode, StringComparison.OrdinalIgnoreCase))
                        {
                            p2.MarkCancelledByCaller();
                            _activeIncoming = null;
                            _activeRoomCode = string.Empty;
                        }
                    }
                }
                catch
                {
                }
            }
            catch
            {
            }
        });
    }
}
