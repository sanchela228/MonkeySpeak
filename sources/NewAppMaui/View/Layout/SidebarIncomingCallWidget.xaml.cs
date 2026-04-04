namespace NewAppMaui.View.Layout;

public partial class SidebarIncomingCallWidget : ContentView
{
    public event EventHandler? Accepted;
    public event EventHandler? Rejected;

    public SidebarIncomingCallWidget()
    {
        InitializeComponent();
    }

    public void Show(string callerName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IncomingCallerLabel.Text = callerName;
            IsVisible = true;
        });
    }

    public void Hide()
    {
        MainThread.BeginInvokeOnMainThread(() => IsVisible = false);
    }

    private void OnAcceptClicked(object? sender, EventArgs e)
    {
        Accepted?.Invoke(this, EventArgs.Empty);
    }

    private void OnRejectClicked(object? sender, EventArgs e)
    {
        Rejected?.Invoke(this, EventArgs.Empty);
    }
}
