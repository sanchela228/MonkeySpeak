using System.Collections.ObjectModel;
using Core.Public.Services;
using Core.Websockets;
using Core.Websockets.Messages.NoAuthCall;
using Microsoft.Extensions.DependencyInjection;

namespace NewAppMaui;

public partial class CallRoomPage : ContentPage
{
    private readonly IConnectionService _connection;
    private readonly ObservableCollection<ParticipantVm> _participants = new();
    private CancellationTokenSource? _cts;

    private string _roomCode = string.Empty;
    private bool _ended;
    private bool _hasEverHadRemote;

    public CallRoomPage()
    {
        _connection = ((App)Application.Current!).Services.GetRequiredService<IConnectionService>();
        InitializeComponent();

        ParticipantsView.ItemsSource = _participants;
    }

    public void InitializeRoom(string roomCode, IEnumerable<InterlocutorJoined>? initialParticipants)
    {
        _roomCode = roomCode ?? string.Empty;

        if (initialParticipants is not null)
        {
            foreach (var p in initialParticipants)
                UpsertParticipant(p.Id, p.IpEndPoint);
        }

        if (_participants.Count > 0)
            _hasEverHadRemote = true;

        RefreshUi();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _cts = new CancellationTokenSource();

        _connection.MessageReceived += OnMessageReceived;
        _connection.StateChanged += OnStateChanged;

        RefreshUi();
    }

    protected override void OnDisappearing()
    {
        _connection.MessageReceived -= OnMessageReceived;
        _connection.StateChanged -= OnStateChanged;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        base.OnDisappearing();
    }

    private void OnStateChanged(object? sender, System.Data.ConnectionState e)
    {
        MainThread.BeginInvokeOnMainThread(RefreshUi);
    }

    private void OnMessageReceived(object? sender, string raw)
    {
        MainThread.BeginInvokeOnMainThread(() => { LastMessageLabel.Text = raw; });

        Context? ctx;
        try
        {
            ctx = System.Text.Json.JsonSerializer.Deserialize<Context>(raw, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return;
        }

        if (ctx is null)
            return;

        try
        {
            var msg = ctx.ToMessage();

            switch (msg)
            {
                case InterlocutorJoined joined:
                    UpsertParticipant(joined.Id, joined.IpEndPoint);
                    _hasEverHadRemote = true;
                    break;

                case InterlocutorLeft left:
                    RemoveParticipant(left.InterlocutorId);
                    break;
            }
        }
        catch
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshUi();
            _ = MaybeAutoHangupAsync();
        });
    }

    private void UpsertParticipant(string id, string ipEndPoint)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        var existing = _participants.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
        if (existing is null)
        {
            _participants.Add(new ParticipantVm { Id = id, IpEndPoint = ipEndPoint ?? string.Empty });
        }
        else
        {
            existing.IpEndPoint = ipEndPoint ?? string.Empty;
        }
    }

    private void RemoveParticipant(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        var existing = _participants.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
        if (existing is not null)
            _participants.Remove(existing);
    }

    private void RefreshUi()
    {
        RoomCodeLabel.Text = string.IsNullOrWhiteSpace(_roomCode) ? "-" : _roomCode;
        WsStateLabel.Text = _connection.State.ToString();
        ParticipantsCountLabel.Text = _participants.Count.ToString();
    }

    private async Task MaybeAutoHangupAsync()
    {
        if (_ended)
            return;

        // Important: don't auto-end while you're still waiting for the first join.
        if (!_hasEverHadRemote)
            return;

        // We track only remote participants here.
        if (_participants.Count > 0)
            return;

        await EndCallAsync("Alone");
    }

    private async Task EndCallAsync(string reason)
    {
        if (_ended)
            return;

        _ended = true;

        try
        {
            var ct = _cts?.Token ?? CancellationToken.None;
            var hangup = Context.Create(new HangupSession { Value = reason });
            await _connection.SendAsync(System.Text.Json.JsonSerializer.Serialize(hangup), ct);
        }
        catch
        {
        }

        try
        {
            await Navigation.PopModalAsync();
        }
        catch
        {
        }

        try
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
        catch
        {
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await EndCallAsync("UserHangup");
    }

    private sealed class ParticipantVm
    {
        public string Id { get; set; } = string.Empty;
        public string IpEndPoint { get; set; } = string.Empty;
    }
}
