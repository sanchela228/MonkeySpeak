namespace NewAppMaui.View.Pages.Content;

public partial class P2PCallTestPage2 : ContentView
{
    public P2PCallTestPage2()
    {
        InitializeComponent();
    }
    
    private async void OnTestBackSettingsMenu(object sender, EventArgs e)
    {
        var page = ((App)Application.Current!).Services.GetRequiredService<ConnectionProfileSelectPage>();
        await Navigation.PushModalAsync(page);
    }
}
