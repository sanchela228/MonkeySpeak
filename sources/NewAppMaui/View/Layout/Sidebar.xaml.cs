using Core.Public.Services;

namespace NewAppMaui.View.Layout;

public partial class Sidebar : ContentView
{
    public event Action<string>? MenuItemSelected;
    public event Action? CollapseRequested;
    public event Action? CallHangupRequested;
    public event Action? CallMicToggled;
    public event Action? CallVolumeToggled;

    private IDispatcherTimer? _callTimer;
    private DateTime _callStartTime;
    private bool _micEnabled = true;
    private bool _volumeEnabled = true;

    private static readonly Color ActiveBg = Color.FromArgb("#14251d");
    private static readonly Color ActiveText = Color.FromArgb("#22c55e");
    private static readonly Color InactiveText = Colors.White;

    public Sidebar()
    {
        InitializeComponent();
        SetActiveItem("menu1");
    }

    public void SetUsername(string username)
    {
        UsernameLabel.Text = username;
    }

    public void SetCallActive(bool active, string roomCode = "")
    {
        CallMenuItem.IsVisible = active;

        if (active)
        {
            CallRoomCodeLabel.Text = roomCode.ToUpperInvariant();
            CallParticipantsLabel.Text = "1 in call";
            _callStartTime = DateTime.Now;
            _micEnabled = true;
            _volumeEnabled = true;
            UpdateMicIcon();
            UpdateVolumeIcon();
            StartCallTimer();
        }
        else
        {
            StopCallTimer();
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
        _micEnabled = enabled;
        MainThread.BeginInvokeOnMainThread(UpdateMicIcon);
    }

    public void SyncVolumeState(bool enabled)
    {
        _volumeEnabled = enabled;
        MainThread.BeginInvokeOnMainThread(UpdateVolumeIcon);
    }

    public void SetActiveItem(string itemKey)
    {
        var inactive = Colors.Transparent;

        MenuItem1.BackgroundColor = itemKey == "menu1" ? ActiveBg : inactive;
        MenuItem1Label.TextColor = itemKey == "menu1" ? ActiveText : InactiveText;
        MenuItem1Icon.Source = itemKey == "menu1" ? "phone_green.png" : "phone.png";

        MenuItem2.BackgroundColor = itemKey == "menu2" ? Color.FromArgb("#1a1a1a") : inactive;
        MenuItem2Label.TextColor = itemKey == "menu2" ? Color.FromArgb("#9ca3af") : InactiveText;
    }

    private void StartCallTimer()
    {
        StopCallTimer();
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

    private void StopCallTimer()
    {
        _callTimer?.Stop();
        _callTimer = null;
        CallTimerLabel.Text = "00:00";
    }

    private void UpdateMicIcon()
    {
        CallMicIcon.Source = _micEnabled ? "micro.png" : "micro_muted.png";
    }

    private void UpdateVolumeIcon()
    {
        CallVolumeIcon.Source = _volumeEnabled ? "volume.png" : "volume_muted.png";
    }

    private void OnMenu1Tapped(object? sender, TappedEventArgs e)
    {
        SetActiveItem("menu1");
        MenuItemSelected?.Invoke("menu1");
    }

    private void OnMenu2Tapped(object? sender, TappedEventArgs e)
    {
        SetActiveItem("menu2");
        MenuItemSelected?.Invoke("menu2");
    }

    private void OnCallTapped(object? sender, TappedEventArgs e)
    {
        MenuItemSelected?.Invoke("call");
    }

    private void OnCallMicTapped(object? sender, TappedEventArgs e)
    {
        _micEnabled = !_micEnabled;
        UpdateMicIcon();
        CallMicToggled?.Invoke();
    }

    private void OnCallVolumeTapped(object? sender, TappedEventArgs e)
    {
        _volumeEnabled = !_volumeEnabled;
        UpdateVolumeIcon();
        CallVolumeToggled?.Invoke();
    }

    private void OnCallHangupTapped(object? sender, TappedEventArgs e)
    {
        CallHangupRequested?.Invoke();
    }

    private void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        MenuItemSelected?.Invoke("settings");
    }

    private void OnCollapseTapped(object? sender, TappedEventArgs e)
    {
        CollapseRequested?.Invoke();
    }
}
