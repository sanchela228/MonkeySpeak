using System.ComponentModel;
using System.Windows.Input;
using Core.Public.Configurations;
using NewAppMaui.View.Layout;
using NewAppMaui.View.Pages;
using ConnectionProfile = Core.Public.Configurations.ConnectionProfile;

namespace NewAppMaui;

public partial class ConnectionProfileSelectPage : ContentPage, INotifyPropertyChanged
{
    private readonly IConnectionSettingsStore _store;
    private readonly bool _startupMode;
    private readonly IServiceProvider _services;

    private string _selectedProfileId;

    public string SelectedProfileId
    {
        get => _selectedProfileId;
        set
        {
            if (_selectedProfileId == value)
                return;

            _selectedProfileId = value;
            OnPropertyChanged();
        }
    }

    public ICommand SelectProfileCommand { get; }

    public ConnectionProfileSelectPage(IConnectionSettingsStore store, IServiceProvider services, CallUiController callController)
    {
        _store = store;
        _services = services;
        _startupMode = !_store.HasExplicitActiveProfileSelection;
        _selectedProfileId = string.Empty;

        if (callController.IsInCall)
            _ = callController.EndCallAsync("ServerChange");

        SelectProfileCommand = new Command<ConnectionProfile>(profile =>
        {
            if (profile is not null)
                SelectedProfileId = profile.Id;
        });

        InitializeComponent();
        BindingContext = this;
    }

    private async Task RefreshAsync()
    {
        var profiles = await _store.GetProfilesAsync();
        BindableLayout.SetItemsSource(ProfilesView, profiles);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();

        var active = await _store.GetActiveAsync();
        if (active is not null)
            SelectedProfileId = active.Id;

        if (Application.Current?.Resources.TryGetValue("LastCreatedConnectionProfileId", out var value) == true
            && value is string id
            && !string.IsNullOrWhiteSpace(id))
        {
            Application.Current.Resources.Remove("LastCreatedConnectionProfileId");
            SelectedProfileId = id;
        }
    }

    private ConnectionProfile? GetSelectedProfile()
    {
        if (string.IsNullOrEmpty(SelectedProfileId))
            return null;

        if (BindableLayout.GetItemsSource(ProfilesView) is not IEnumerable<ConnectionProfile> profiles)
            return null;

        return profiles.FirstOrDefault(p => string.Equals(p.Id, SelectedProfileId, StringComparison.Ordinal));
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var selected = GetSelectedProfile();
        if (selected is null)
            return;

        await _store.DeleteUserProfileAsync(selected.Id);
        SelectedProfileId = string.Empty;
        await RefreshAsync();
    }

    private async void OnSelectClicked(object? sender, EventArgs e)
    {
        var selected = GetSelectedProfile();
        if (selected is null)
            return;

        await _store.SetActiveAsync(selected.Id);

        if (Window is not null)
            Window.Page = _services.GetRequiredService<ConnectingPage>();
    }

    private async void OnAddCustomClicked(object? sender, EventArgs e)
    {
        var page = _services.GetRequiredService<ConnectionProfileCreatePage>();
        await Navigation.PushModalAsync(new NavigationPage(page));
    }

    private void OnAddCustomTapped(object? sender, TappedEventArgs e)
    {
        OnAddCustomClicked(sender, e);
    }
}
