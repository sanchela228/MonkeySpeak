using System.Linq;
using Core.Public.Services;
using Microsoft.Extensions.DependencyInjection;

namespace NewAppMaui;

public partial class FriendsPage : ContentPage
{
    private readonly IFriendsService _friends;
    private CancellationTokenSource? _cts;

    public FriendsPage()
    {
        _friends = ((App)Application.Current!).Services.GetRequiredService<IFriendsService>();
        InitializeComponent();

        _friends.FriendsUpdated += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateLists);
        _friends.PendingUpdated += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateLists);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _cts = new CancellationTokenSource();
        UpdateLists();
        _ = RefreshAsync(_cts.Token);
    }

    protected override void OnDisappearing()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnDisappearing();
    }

    private void UpdateLists()
    {
        FriendsView.ItemsSource = _friends.Friends.ToArray();
        PendingView.ItemsSource = _friends.Pending.ToArray();
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = string.Empty;
        });

        try
        {
            await _friends.RefreshAsync(ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ErrorLabel.Text = ex.Message;
                ErrorLabel.IsVisible = true;
            });
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        var ct = _cts?.Token ?? CancellationToken.None;
        await RefreshAsync(ct);
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var ct = _cts?.Token ?? CancellationToken.None;

        var username = (UsernameEntry.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(username))
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = string.Empty;
        });

        try
        {
            await _friends.SendFriendRequestAsync(username, ct);
            await MainThread.InvokeOnMainThreadAsync(() => UsernameEntry.Text = string.Empty);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ErrorLabel.Text = ex.Message;
                ErrorLabel.IsVisible = true;
            });
        }
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn)
            return;

        var friendshipId = btn.CommandParameter as string;
        if (string.IsNullOrWhiteSpace(friendshipId))
            return;

        var ct = _cts?.Token ?? CancellationToken.None;

        try
        {
            await _friends.AcceptAsync(friendshipId, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ErrorLabel.Text = ex.Message;
                ErrorLabel.IsVisible = true;
            });
        }
    }

    private async void OnRejectClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn)
            return;

        var friendshipId = btn.CommandParameter as string;
        if (string.IsNullOrWhiteSpace(friendshipId))
            return;

        var ct = _cts?.Token ?? CancellationToken.None;

        try
        {
            await _friends.RejectAsync(friendshipId, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ErrorLabel.Text = ex.Message;
                ErrorLabel.IsVisible = true;
            });
        }
    }

    private async void OnRemoveClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn)
            return;

        var friendId = btn.CommandParameter as string;
        if (string.IsNullOrWhiteSpace(friendId))
            return;

        var ct = _cts?.Token ?? CancellationToken.None;

        try
        {
            await _friends.RemoveFriendAsync(friendId, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ErrorLabel.Text = ex.Message;
                ErrorLabel.IsVisible = true;
            });
        }
    }
}
