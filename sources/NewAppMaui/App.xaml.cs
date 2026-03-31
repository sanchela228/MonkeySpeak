using Core.Application.Abstractions;
using Core.Public.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace NewAppMaui;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public IServiceProvider Services => _services;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        if (Environment.GetEnvironmentVariable("MONKEYSPEAK_DEV_NEW_USER_EACH_RUN") == "1")
        {
            try
            {
                if (_services.GetService<IKeyStore>() is MauiKeyStore ks)
                    ks.ClearAllLocal();
            }
            catch
            {
            }
        }

        var store = _services.GetRequiredService<IConnectionSettingsStore>();
        Page rootPage;
        if (store.HasExplicitActiveProfileSelection)
            rootPage = new AppShell();
        else
            rootPage = _services.GetRequiredService<ConnectionProfileSelectPage>();

        _ = _services.GetRequiredService<FriendCallsUiCoordinator>();

        return new Window(rootPage);
    }
}