using Core.Public.Configurations;

namespace NewAppMaui;

public partial class ConnectionProfileCreatePage : ContentPage
{
    private readonly IConnectionSettingsStore _store;

    public ConnectionProfileCreatePage(IConnectionSettingsStore store)
    {
        _store = store;
        InitializeComponent();

        NameEntry.Text = "My server";
        DomainEntry.Text = "example.com";
        PortEntry.Text = "80";
        UseSslSwitch.IsToggled = false;
        MaxRetriesEntry.Text = "2";
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        if (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync();
        else
            await Navigation.PopAsync();
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert("Validation", "Name is required.", "OK");
            return;
        }

        var domain = DomainEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(domain))
        {
            await DisplayAlert("Validation", "Domain is required.", "OK");
            return;
        }

        if (!int.TryParse(PortEntry.Text, out var port) || port <= 0 || port > 65535)
        {
            await DisplayAlert("Validation", "Port must be a number between 1 and 65535.", "OK");
            return;
        }

        if (!int.TryParse(MaxRetriesEntry.Text, out var maxRetries) || maxRetries < 0)
        {
            await DisplayAlert("Validation", "Max retries must be 0 or greater.", "OK");
            return;
        }

        var profile = new Core.Public.Configurations.ConnectionProfile(
            Id: Guid.NewGuid().ToString("N"),
            Name: name,
            Domain: domain,
            Port: port,
            UseSsl: UseSslSwitch.IsToggled,
            MaxRetries: maxRetries);

        await _store.UpsertUserProfileAsync(profile);

        if (Application.Current is not null)
            Application.Current.Resources["LastCreatedConnectionProfileId"] = profile.Id;

        await _store.SetActiveAsync(profile.Id);

        if (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync();
        else
            await Navigation.PopAsync();
    }
}
