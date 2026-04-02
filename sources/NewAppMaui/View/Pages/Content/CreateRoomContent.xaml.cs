using Core.Domain.Calls;
using Core.Public.Services;

namespace NewAppMaui.View.Pages.Content;

public partial class CreateRoomContent : ContentView
{
    private readonly IConnectionService _connection;
    private readonly ICallsService _calls;
    private CancellationTokenSource? _cts;
    private string _roomCode = string.Empty;
    private bool _navigatedToCall;

    public event Action? BackRequested;
    public event Action<string, Core.Websockets.Messages.NoAuthCall.InterlocutorJoined[]>? RoomConnected;

    public CreateRoomContent()
    {
        var services = ((App)Application.Current!).Services;
        _connection = services.GetRequiredService<IConnectionService>();
        _calls = services.GetRequiredService<ICallsService>();

        InitializeComponent();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null)
        {
            _navigatedToCall = false;
            _calls.StateChanged += OnCallStateChanged;
            _cts = new CancellationTokenSource();
            _ = StartAsync(_cts.Token);
        }
        else
        {
            _calls.StateChanged -= OnCallStateChanged;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task StartAsync(CancellationToken ct)
    {
        ShowLoading("Creating room...");

        if (_connection.State != System.Data.ConnectionState.Open)
        {
            ShowError("WebSocket is not connected.");
            return;
        }

        try
        {
            var session = await _calls.CreateAsync(ct).ConfigureAwait(false);
            var code = session.RoomCode;
            _roomCode = code;

            try { await Clipboard.Default.SetTextAsync(code); } catch { }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Spinner.IsRunning = false;
                Spinner.IsVisible = false;
                StatusLabel.Text = "Waiting for participants...";
                CodeLabel.Text = code.ToUpperInvariant();
                CodeLabel.IsVisible = true;
                CopiedLabel.IsVisible = true;
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowLoading(string text)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Spinner.IsRunning = true;
            Spinner.IsVisible = true;
            StatusLabel.Text = text;
            CodeLabel.IsVisible = false;
            CopiedLabel.IsVisible = false;
            ErrorLabel.IsVisible = false;
        });
    }

    private void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Spinner.IsRunning = false;
            Spinner.IsVisible = false;
            StatusLabel.IsVisible = false;
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
        });
    }

    private void OnCallStateChanged(object? sender, CallState state)
    {
        if (state != CallState.Connected || _navigatedToCall)
            return;

        _navigatedToCall = true;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var session = _calls.Current;
            if (session is null) return;

            var initial = session.Interlocutors
                .Select(x => new Core.Websockets.Messages.NoAuthCall.InterlocutorJoined
                {
                    Id = x.Id,
                    IpEndPoint = x.RemoteIp.ToString()
                })
                .ToArray();

            RoomConnected?.Invoke(session.RoomCode, initial);
        });
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        BackRequested?.Invoke();
    }
}
