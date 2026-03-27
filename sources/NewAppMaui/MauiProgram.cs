using Core.Application.Services;
using Core.Public;
using Core.Public.Configurations;
using Core.Public.Services;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Extensions.Logging;
using NewAppMaui.Configurations;
#if WINDOWS
using Microsoft.Maui.Platform;
using Microsoft.UI.Windowing;
using Windows.Graphics;
#endif

namespace NewAppMaui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

#if WINDOWS
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddWindows(windows =>
            {
                windows.OnWindowCreated(window =>
                {
                    const string widthKey = "window.width";
                    const string heightKey = "window.height";

                    var appWindow = window.GetAppWindow();
                    if (appWindow is null)
                        return;

                    var width = Microsoft.Maui.Storage.Preferences.Get(widthKey, 800);
                    var height = Microsoft.Maui.Storage.Preferences.Get(heightKey, 600);
                    appWindow.Resize(new SizeInt32(width, height));

                    appWindow.Changed += (_, args) =>
                    {
                        if (!args.DidSizeChange)
                            return;

                        var size = appWindow.Size;
                        Microsoft.Maui.Storage.Preferences.Set(widthKey, size.Width);
                        Microsoft.Maui.Storage.Preferences.Set(heightKey, size.Height);
                    };
                });
            });
        });
#endif
        
        builder.Services.AddSingleton<IConnectionSettingsStore, ConnectionSettingsStore>();

        builder.Services.AddTransient<ConnectionProfileSelectPage>();
        builder.Services.AddTransient<ConnectionProfileCreatePage>();
        builder.Services.AddTransient<MainPage>();

        builder.Services.AddSingleton<IConnectionService, ConnectionService>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IFriendsService, FriendsService>();
        builder.Services.AddSingleton<ICallsService, CallsService>();
        
        // register DI MonkeySpeakClient
        builder.Services.AddSingleton<IMonkeySpeakClient>(sp => new MonkeySpeakClient(
            sp.GetRequiredService<IConnectionService>(),
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IFriendsService>(),
            sp.GetRequiredService<ICallsService>()
        ));
        
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}