using Core.Public.Configurations;

namespace NewAppMaui;

public partial class ConnectionProfileSelectPage : ContentPage
{
    private readonly IConnectionSettingsStore _store;
    private readonly bool _startupMode;
    private readonly IServiceProvider _services;

    public ConnectionProfileSelectPage(IConnectionSettingsStore store, IServiceProvider services)
    {
        _store = store;
        _services = services;
        _startupMode = !_store.HasExplicitActiveProfileSelection;
        InitializeComponent();
    }

    private async Task RefreshAsync()
    {
        ProfilesView.ItemsSource = await _store.GetProfilesAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();

        if (Application.Current?.Resources.TryGetValue("LastCreatedConnectionProfileId", out var value) == true
            && value is string id
            && !string.IsNullOrWhiteSpace(id))
        {
            Application.Current.Resources.Remove("LastCreatedConnectionProfileId");
            var profiles = await _store.GetProfilesAsync();
            ProfilesView.SelectedItem = profiles.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (ProfilesView.SelectedItem is not Core.Public.Configurations.ConnectionProfile selected)
            return;

        await _store.DeleteUserProfileAsync(selected.Id);
        await RefreshAsync();
    }

    private async void OnSelectClicked(object? sender, EventArgs e)
    {
        if (ProfilesView.SelectedItem is not Core.Public.Configurations.ConnectionProfile selected)
            return;

        await _store.SetActiveAsync(selected.Id);

        if (_startupMode)
        {
            if (Window is not null)
                Window.Page = new AppShell();
            return;
        }

        if (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync();
    }

    private async void OnAddCustomClicked(object? sender, EventArgs e)
    {
        var page = _services.GetRequiredService<ConnectionProfileCreatePage>();
        await Navigation.PushModalAsync(new NavigationPage(page));
    }
}
