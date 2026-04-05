using Core.Public.Services;

namespace NewAppMaui.View.Pages.Content;

public partial class JoinRoomContent : ContentView
{
    private readonly IConnectionService _connection;
    private readonly ICallsService _calls;
    private readonly Entry[] _entries;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _autoConnectCts;
    private bool _suppressTextChanged;
    private bool _connecting;

    private static readonly Color BorderNormal = Color.FromArgb("#151515");
    private static readonly Color BorderSuccess = Color.FromArgb("#4B9B58");
    private static readonly Color BorderError = Color.FromArgb("#ef4444");

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

        SetAllBorders(BorderNormal);

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
            _autoConnectCts?.Cancel();
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

        SetAllBorders(BorderNormal);
        _autoConnectCts?.Cancel();

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
                TryScheduleAutoConnect();
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

    private void TryScheduleAutoConnect()
    {
        var code = GetCode();
        if (code.Length != _entries.Length || _connecting) return;

        _autoConnectCts?.Cancel();
        _autoConnectCts = new CancellationTokenSource();
        var token = _autoConnectCts.Token;

        _ = Task.Run(async () =>
        {
            await Task.Delay(300, token);
            if (!token.IsCancellationRequested)
                MainThread.BeginInvokeOnMainThread(() => _ = ConnectAsync());
        }, token);
    }

    private async Task ConnectAsync()
    {
        if (_connecting) return;
        _connecting = true;

        var ct = _cts?.Token ?? CancellationToken.None;

        var spinnerCts = new CancellationTokenSource();
        _ = ShowSpinnerAfterDelay(500, spinnerCts.Token);

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

            spinnerCts.Cancel();

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                SetAllBorders(BorderSuccess);
                HideSpinner();
                await Task.Delay(300);
                RoomConnected?.Invoke(session.RoomCode, initial);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            spinnerCts.Cancel();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetAllBorders(BorderError);
                HideSpinner();
            });
        }
        finally
        {
            _connecting = false;
        }
    }

    private async Task ShowSpinnerAfterDelay(int ms, CancellationToken ct)
    {
        try
        {
            await Task.Delay(ms, ct);
            if (!ct.IsCancellationRequested)
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Spinner.IsVisible = true;
                    Spinner.IsRunning = true;
                });
        }
        catch (OperationCanceledException) { }
    }

    private void HideSpinner()
    {
        Spinner.IsRunning = false;
        Spinner.IsVisible = false;
    }

    private void SetAllBorders(Color color)
    {
#if WINDOWS
        foreach (var entry in _entries)
        {
            if (entry.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox tb)
            {
                var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(color.Alpha.ToByte(), color.Red.ToByte(), color.Green.ToByte(), color.Blue.ToByte()));
                tb.BorderBrush = brush;
                tb.BorderThickness = new Microsoft.UI.Xaml.Thickness(1.5);
                tb.Resources["TextControlBorderBrush"] = brush;
                tb.Resources["TextControlBorderBrushPointerOver"] = brush;
                tb.Resources["TextControlBorderBrushFocused"] = brush;
            }
        }
#endif
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        _autoConnectCts?.Cancel();
        BackRequested?.Invoke();
    }
}

static class ColorByteExtensions
{
    public static byte ToByte(this float f) => (byte)(f * 255);
}
