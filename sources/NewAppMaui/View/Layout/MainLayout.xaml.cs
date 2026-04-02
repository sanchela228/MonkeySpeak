using Core.Public.Services;

namespace NewAppMaui.View.Layout;

public partial class MainLayout : ContentPage
{
    private readonly IAuthService _auth;
    private readonly Dictionary<string, Func<Microsoft.Maui.Controls.View>> _contentFactories;
    private bool _sidebarCollapsed;

    public MainLayout(IAuthService auth)
    {
        _auth = auth;

        InitializeComponent();

        _contentFactories = new()
        {
            ["menu1"] = () => new Pages.Content.P2PCallTestPage(),
            ["menu2"] = () => new Pages.Content.P2PCallTestPage2(),
        };

        SidebarView.MenuItemSelected += OnMenuItemSelected;
        SidebarView.CollapseRequested += () => SetSidebarCollapsed(true);

        NavigateTo("menu1");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_auth.IsAuthenticated && _auth.Username is not null)
            SidebarView.SetUsername(_auth.Username);
    }

    public void NavigateTo(string key)
    {
        if (_contentFactories.TryGetValue(key, out var factory))
        {
            ContentArea.Content = factory();
            SidebarView.SetActiveItem(key);
        }
    }

    private void SetSidebarCollapsed(bool collapsed)
    {
        _sidebarCollapsed = collapsed;

        SidebarView.IsVisible = !collapsed;
        SidebarSeparator.IsVisible = !collapsed;
        ExpandButton.IsVisible = collapsed;

        RootGrid.ColumnDefinitions[0].Width = collapsed
            ? new GridLength(0)
            : new GridLength(260);
    }

    private void OnExpandTapped(object? sender, TappedEventArgs e)
    {
        SetSidebarCollapsed(false);
    }

    private void OnMenuItemSelected(string key)
    {
        NavigateTo(key);
    }
}
