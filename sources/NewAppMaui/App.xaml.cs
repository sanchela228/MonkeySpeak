using Core.Public.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace NewAppMaui;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var store = _services.GetRequiredService<IConnectionSettingsStore>();
        Page rootPage;
        if (store.HasExplicitActiveProfileSelection)
            rootPage = new AppShell();
        else
            rootPage = _services.GetRequiredService<ConnectionProfileSelectPage>();

        return new Window(rootPage);
    }
}