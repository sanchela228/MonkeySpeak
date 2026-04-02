using NewAppMaui.View.Layout;

namespace NewAppMaui.View.Pages.Content;

public partial class P2PCallTestPage : ContentView
{
    public P2PCallTestPage()
    {
        InitializeComponent();
    }

    private void OnCreateRoomClicked(object? sender, EventArgs e)
    {
        GetMainLayout()?.ShowCreateRoom();
    }

    private void OnJoinRoomClicked(object? sender, EventArgs e)
    {
        GetMainLayout()?.ShowJoinRoom();
    }

    private MainLayout? GetMainLayout()
    {
        var element = this.Parent;
        while (element is not null)
        {
            if (element is MainLayout layout)
                return layout;
            element = (element as Element)?.Parent;
        }
        return null;
    }
}
