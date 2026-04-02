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

        foreach (var entry in _entries)
            AttachBackspaceHandler(entry);
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

    private void AttachBackspaceHandler(Entry entry)
    {
        entry.HandlerChanged += (_, _) =>
        {
#if WINDOWS
            if (entry.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox textBox)
            {
                textBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Windows.System.VirtualKey.Back && string.IsNullOrEmpty(textBox.Text))
                    {
                        var idx = Array.IndexOf(_entries, entry);
                        if (idx > 0)
                        {
                            var prev = _entries[idx - 1];
                            prev.Focus();
                            prev.CursorPosition = prev.Text?.Length ?? 0;
                        }
                        e.Handled = true;
                    }
                };
            }
#endif
        };
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
            {
                var next = _entries[idx + 1];
                next.Focus();
                if (!string.IsNullOrEmpty(next.Text))
                {
                    next.CursorPosition = 0;
                    next.SelectionLength = next.Text.Length;
                }
            }
            else
            {
                CheckAutoConnect();
            }
        }
        else
        {
            if (idx > 0)
                _entries[idx - 1].Focus();
        }
    }

    private void OnCharFocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry && !string.IsNullOrEmpty(entry.Text))
        {
            entry.CursorPosition = 0;
            entry.SelectionLength = entry.Text.Length;
        }
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

        Spinner.IsVisible = true;
        Spinner.IsRunning = true;
        ConnectButton.IsEnabled = false;

        try
        {
            if (_connection.State != System.Data.ConnectionState.Open)
                throw new InvalidOperationException("WebSocket is not connected.");

            var code = GetCode().Trim().ToLowerInvariant();
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
