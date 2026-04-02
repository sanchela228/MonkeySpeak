namespace NewAppMaui.View.Pages.Content;

public partial class P2PCallTestPage : ContentView
{
    public P2PCallTestPage()
    {
        InitializeComponent();
    }
    
    private async void OnTestBackSettingsMenu(object sender, EventArgs e)
    {
        var page = ((App)Application.Current!).Services.GetRequiredService<ConnectionProfileSelectPage>();
        await Navigation.PushModalAsync(page);
    }
}
