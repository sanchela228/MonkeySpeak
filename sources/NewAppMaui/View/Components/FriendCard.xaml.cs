using Core.Domain.Friends;

namespace NewAppMaui.View.Components;

public partial class FriendCard : ContentView
{
    public Friend? FriendData { get; private set; }

    public event EventHandler<Friend>? Clicked;

    public FriendCard()
    {
        InitializeComponent();
    }

    public void SetFriend(Friend friend)
    {
        FriendData = friend;
        NameLabel.Text = friend.Username ?? "Unknown";
        Avatar.Setup(36, friend.Username, friend.UserId);

        if (friend.IsOnline)
        {
            OnlineDot.IsVisible = true;
            StatusLabel.Text = "Online";
            StatusLabel.IsVisible = true;
        }
        else
        {
            OnlineDot.IsVisible = false;
            StatusLabel.IsVisible = false;
        }
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (FriendData is not null)
            Clicked?.Invoke(this, FriendData);
    }
}
