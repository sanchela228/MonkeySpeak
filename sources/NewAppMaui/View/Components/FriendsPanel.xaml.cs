using Core.Domain.Friends;
using Core.Public.Services;

namespace NewAppMaui.View.Components;

public partial class FriendsPanel : ContentView
{
    private IFriendsService? _friends;

    public event EventHandler<Friend>? FriendSelected;
    public event EventHandler? AddFriendRequested;
    public event EventHandler<FriendRequest>? AcceptRequested;
    public event EventHandler<FriendRequest>? RejectRequested;

    public FriendsPanel()
    {
        InitializeComponent();
    }

    public void Initialize(IFriendsService friendsService)
    {
        _friends = friendsService;
        _friends.FriendsUpdated += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        _friends.PendingUpdated += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);

        _ = _friends.RefreshAsync();
    }

    public void Refresh()
    {
        if (_friends is null) return;

        var friends = _friends.Friends ?? [];
        var pending = _friends.Pending ?? [];

        PendingList.Children.Clear();
        OnlineList.Children.Clear();
        OfflineList.Children.Clear();

        // Pending requests
        PendingSection.IsVisible = pending.Count > 0;
        foreach (var req in pending)
        {
            var card = new PendingRequestCard();
            card.SetRequest(req);
            card.AcceptClicked += (_, r) => AcceptRequested?.Invoke(this, r);
            card.RejectClicked += (_, r) => RejectRequested?.Invoke(this, r);
            PendingList.Children.Add(card);
        }

        // Split friends
        var online = friends.Where(f => f.IsOnline).ToList();
        var offline = friends.Where(f => !f.IsOnline).ToList();

        OnlineSection.IsVisible = online.Count > 0;
        foreach (var f in online)
        {
            var card = new FriendCard();
            card.SetFriend(f);
            card.Clicked += (_, friend) => FriendSelected?.Invoke(this, friend);
            OnlineList.Children.Add(card);
        }

        OfflineSection.IsVisible = offline.Count > 0;
        foreach (var f in offline)
        {
            var card = new FriendCard();
            card.SetFriend(f);
            card.Clicked += (_, friend) => FriendSelected?.Invoke(this, friend);
            OfflineList.Children.Add(card);
        }

        EmptyLabel.IsVisible = friends.Count == 0 && pending.Count == 0;
    }

    private void OnAddFriendTapped(object? sender, TappedEventArgs e)
    {
        AddFriendRequested?.Invoke(this, EventArgs.Empty);
    }
}
