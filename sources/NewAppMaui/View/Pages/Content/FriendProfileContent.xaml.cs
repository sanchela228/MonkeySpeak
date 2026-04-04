using Core.Domain.Friends;
using Core.Public.Services;

namespace NewAppMaui.View.Pages.Content;

public partial class FriendProfileContent : ContentView
{
    private readonly IFriendsService _friends;
    private Friend? _friend;

    public event Action? BackRequested;
    public event Action<string, string>? CallRequested;

    public FriendProfileContent()
    {
        var services = ((App)Application.Current!).Services;
        _friends = services.GetRequiredService<IFriendsService>();
        InitializeComponent();
    }

    public void SetFriend(Friend friend)
    {
        _friend = friend;

        var name = friend.Username ?? "Unknown";
        NameLabel.Text = name;
        Avatar.Setup(80, name, friend.UserId);

        if (friend.IsOnline)
        {
            OnlineDot.IsVisible = true;
            StatusLabel.Text = "Online";
            StatusLabel.TextColor = Color.FromArgb("#22c55e");
            StatusLabel.IsVisible = true;
        }
        else
        {
            OnlineDot.IsVisible = false;
            StatusLabel.Text = "Offline";
            StatusLabel.TextColor = Color.FromArgb("#9ca3af");
            StatusLabel.IsVisible = true;
        }
    }

    private void OnCallClicked(object? sender, EventArgs e)
    {
        if (_friend is null) return;
        CallRequested?.Invoke(_friend.UserId, _friend.Username ?? "Unknown");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_friend is null) return;

        try
        {
            await _friends.RemoveFriendAsync(_friend.UserId);
            BackRequested?.Invoke();
        }
        catch (Exception ex)
        {
            Core.Logger.Warn($"RemoveFriend failed: {ex.Message}");
        }
    }
}
