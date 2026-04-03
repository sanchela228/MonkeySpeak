using Core.Domain.Friends;

namespace NewAppMaui.View.Components;

public partial class PendingRequestCard : ContentView
{
    public FriendRequest? RequestData { get; private set; }

    public event EventHandler<FriendRequest>? AcceptClicked;
    public event EventHandler<FriendRequest>? RejectClicked;

    public PendingRequestCard()
    {
        InitializeComponent();
    }

    public void SetRequest(FriendRequest request)
    {
        RequestData = request;
        NameLabel.Text = request.FromUsername ?? "Unknown";
        var name = request.FromUsername ?? "";
        AvatarLabel.Text = name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }

    private void OnAcceptTapped(object? sender, TappedEventArgs e)
    {
        if (RequestData is not null)
            AcceptClicked?.Invoke(this, RequestData);
    }

    private void OnRejectTapped(object? sender, TappedEventArgs e)
    {
        if (RequestData is not null)
            RejectClicked?.Invoke(this, RequestData);
    }
}
