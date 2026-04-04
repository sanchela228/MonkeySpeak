namespace NewAppMaui.View.Layout;

public partial class SidebarActiveCallWidget : ContentView
{
    public event Action? MicToggled;
    public event Action? VolumeToggled;
    public event Action? HangupRequested;
    public event Action? CallTapped;

    private IDispatcherTimer? _callTimer;
    private DateTime _callStartTime;

    public SidebarActiveCallWidget()
    {
        InitializeComponent();
    }

    public void SetActive(bool active, string roomCode = "")
    {
        IsVisible = active;

        if (active)
        {
            CallRoomCodeLabel.Text = roomCode.ToUpperInvariant();
            CallParticipantsLabel.Text = "1 in call";
            _callStartTime = DateTime.Now;
            StartTimer();
        }
        else
        {
            StopTimer();
        }
    }

    public void UpdateParticipantCount(int count)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CallParticipantsLabel.Text = count == 1 ? "1 in call" : $"{count} in call";
        });
    }

    public void SyncMicState(bool enabled)
    {
        CallMicIcon.Source = enabled ? "micro.png" : "micro_muted.png";
    }

    public void SyncVolumeState(bool enabled)
    {
        CallVolumeIcon.Source = enabled ? "volume.png" : "volume_muted.png";
    }

    private void StartTimer()
    {
        StopTimer();
        _callTimer = Dispatcher.CreateTimer();
        _callTimer.Interval = TimeSpan.FromSeconds(1);
        _callTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _callStartTime;
            CallTimerLabel.Text = elapsed.Hours > 0
                ? elapsed.ToString(@"hh\:mm\:ss")
                : elapsed.ToString(@"mm\:ss");
        };
        _callTimer.Start();
    }

    private void StopTimer()
    {
        _callTimer?.Stop();
        _callTimer = null;
        CallTimerLabel.Text = "00:00";
    }

    private void OnCallTapped(object? sender, TappedEventArgs e) => CallTapped?.Invoke();
    private void OnMicTapped(object? sender, TappedEventArgs e) => MicToggled?.Invoke();
    private void OnVolumeTapped(object? sender, TappedEventArgs e) => VolumeToggled?.Invoke();
    private void OnHangupTapped(object? sender, TappedEventArgs e) => HangupRequested?.Invoke();
}
