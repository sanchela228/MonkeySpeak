using Core.Public.Services;

namespace NewAppMaui.View.Layout;

public partial class MainLayout : ContentPage
{
    private readonly IAuthService _auth;
    private readonly Dictionary<string, Func<Microsoft.Maui.Controls.View>> _contentFactories;

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

    private void OnMenuItemSelected(string key)
    {
        NavigateTo(key);
    }
}
