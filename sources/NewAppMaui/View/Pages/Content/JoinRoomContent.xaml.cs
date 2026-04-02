using Core.Public.Services;

namespace NewAppMaui.View.Pages.Content;

public partial class JoinRoomContent : ContentView
{
    private readonly IConnectionService _connection;
    private readonly ICallsService _calls;
    private readonly Entry[] _entries;
    private CancellationTokenSource? _cts;
    private bool _suppressTextChanged;

    public event Action? BackRequested;
    public event Action<string, Core.Websockets.Messages.NoAuthCall.InterlocutorJoined[]>? RoomConnected;

    public JoinRoomContent()
    {
        var services = ((App)Application.Current!).Services;
        _connection = services.GetRequiredService<IConnectionService>();
        _calls = services.GetRequiredService<ICallsService>();

        InitializeComponent();

        _entries = [C0, C1, C2, C3, C4, C5];
        _cts = new CancellationTokenSource();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null)
            C0.Focus();
        else
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void OnCharTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged || sender is not Entry entry)
            return;

        var idx = Array.IndexOf(_entries, entry);
        if (idx < 0) return;

        if (!string.IsNullOrEmpty(e.NewTextValue))
        {
            _suppressTextChanged = true;
            entry.Text = e.NewTextValue[^1..].ToUpperInvariant();
            _suppressTextChanged = false;

            if (idx < _entries.Length - 1)
                _entries[idx + 1].Focus();
            else
                CheckAutoConnect();
        }
        else
        {
            if (idx > 0)
                _entries[idx - 1].Focus();
        }
    }

    private void OnCharFocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry)
            entry.CursorPosition = entry.Text?.Length ?? 0;
    }

    private string GetCode()
    {
        return string.Concat(_entries.Select(e => e.Text ?? ""));
    }

    private void CheckAutoConnect()
    {
        var code = GetCode();
        if (code.Length == _entries.Length)
            _ = ConnectAsync();
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        var ct = _cts?.Token ?? CancellationToken.None;

        // ErrorLabel.IsVisible = false;
        Spinner.IsVisible = true;
        Spinner.IsRunning = true;
        ConnectButton.IsEnabled = false;

        try
        {
            if (_connection.State != System.Data.ConnectionState.Open)
                throw new InvalidOperationException("WebSocket is not connected.");

            var code = GetCode().Trim();
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Please enter a code.");

            var session = await _calls.JoinAsync(code, ct).ConfigureAwait(false);

            var initial = session.Interlocutors
                .Select(x => new Core.Websockets.Messages.NoAuthCall.InterlocutorJoined
                {
                    Id = x.Id,
                    IpEndPoint = x.RemoteIp.ToString()
                })
                .ToArray();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                RoomConnected?.Invoke(session.RoomCode, initial);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // ErrorLabel.Text = ex.Message;
                // ErrorLabel.IsVisible = true;
            });
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Spinner.IsRunning = false;
                Spinner.IsVisible = false;
                ConnectButton.IsEnabled = true;
            });
        }
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        BackRequested?.Invoke();
    }
}
