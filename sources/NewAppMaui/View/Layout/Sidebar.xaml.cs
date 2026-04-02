namespace NewAppMaui.View.Layout;

public partial class Sidebar : ContentView
{
    public event Action<string>? MenuItemSelected;
    public event Action? CollapseRequested;

    public Sidebar()
    {
        InitializeComponent();
        SetActiveItem("menu1");
    }

    public void SetUsername(string username)
    {
        UsernameLabel.Text = username;
    }

    public void SetActiveItem(string itemKey)
    {
        MenuItem1.BackgroundColor = itemKey == "menu1"
            ? Color.FromArgb("#1a1a1a")
            : Colors.Transparent;

        MenuItem2.BackgroundColor = itemKey == "menu2"
            ? Color.FromArgb("#1a1a1a")
            : Colors.Transparent;
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

    private void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        MenuItemSelected?.Invoke("settings");
    }

    private void OnCollapseTapped(object? sender, TappedEventArgs e)
    {
        CollapseRequested?.Invoke();
    }
}
