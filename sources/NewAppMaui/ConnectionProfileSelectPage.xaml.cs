using System.ComponentModel;
using System.Windows.Input;
using Core.Public.Configurations;

namespace NewAppMaui;

public partial class ConnectionProfileSelectPage : ContentPage, INotifyPropertyChanged
{
    private readonly IConnectionSettingsStore _store;
    private readonly bool _startupMode;
    private readonly IServiceProvider _services;
    private bool _suppressSelectionChanged;
    
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

    public ConnectionProfileSelectPage(IConnectionSettingsStore store, IServiceProvider services)
    {
        _store = store;
        _services = services;
        _startupMode = !_store.HasExplicitActiveProfileSelection;
        SelectedProfileId = string.Empty;
        InitializeComponent();
        
        BindingContext = this;
    }

    private async Task RefreshAsync()
    {
        ProfilesView.ItemsSource = await _store.GetProfilesAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();

        var active = await _store.GetActiveAsync();
        if (active is not null)
        {
            try
            {
                _suppressSelectionChanged = true;
                ProfilesView.SelectedItem = active;
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        if (Application.Current?.Resources.TryGetValue("LastCreatedConnectionProfileId", out var value) == true
            && value is string id
            && !string.IsNullOrWhiteSpace(id))
        {
            Application.Current.Resources.Remove("LastCreatedConnectionProfileId");
            var profiles = await _store.GetProfilesAsync();
            try
            {
                _suppressSelectionChanged = true;
                ProfilesView.SelectedItem = profiles.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }
    }

    private async void OnProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged)
            return;

        if (e.CurrentSelection.FirstOrDefault() is not Core.Public.Configurations.ConnectionProfile selected)
            return;
        
        SelectedProfileId = selected.Id;
    }
    
    public ICommand SelectProfileCommand { get; }

    public ConnectionProfileSelectPage()
    {
        InitializeComponent();
        BindingContext = this;

        SelectProfileCommand = new Command<Core.Public.Configurations.ConnectionProfile>(OnProfileSelected);
    }

    private void OnProfileSelected(Core.Public.Configurations.ConnectionProfile item)
    {
        if (item == null)
            return;

        SelectedProfileId = item.Id;
    }

    private void OnAddCustomTapped(object? sender, TappedEventArgs e)
    {
        OnAddCustomClicked(sender, e);
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
