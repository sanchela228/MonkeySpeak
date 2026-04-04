using Core.Application.Services;
using Core.Application.Abstractions;
using Core.Application.Networking;
using Core.Application.Calls.Infrastructure;
using Core.Application.Calls.Infrastructure.Adapters;
using Core.Application.Calls.Networking;
using Core.Public;
using Core.Public.Configurations;
using Core.Public.Services;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.PlatformConfiguration;
using NewAppMaui.Configurations;
using NewAppMaui.Services;

namespace NewAppMaui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Window size will be set in App.CreateWindow via userSettings

        builder.Services.AddSingleton<IConnectionSettingsStore, ConnectionSettingsStore>();
        builder.Services.AddSingleton<IUserSettingsService, UserSettingsService>();

        builder.Services.AddTransient<ConnectionProfileSelectPage>();
        builder.Services.AddTransient<ConnectionProfileCreatePage>();
        builder.Services.AddTransient<View.Pages.ConnectingPage>();
        builder.Services.AddSingleton<View.Layout.CallUiController>();
        builder.Services.AddTransient<View.Layout.MainLayout>();
        builder.Services.AddTransient<CreateRoomPage>();
        builder.Services.AddTransient<JoinRoomPage>();
        builder.Services.AddTransient<CallRoomPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<FriendsPage>();
        builder.Services.AddTransient<OutgoingFriendCallPage>();
        builder.Services.AddTransient<IncomingFriendCallPage>();
        builder.Services.AddTransient<MainPage>();

        builder.Services.AddSingleton<IWebSocketClient, WebSocketClient>();
        builder.Services.AddSingleton<IConnectionService, ConnectionService>();
        builder.Services.AddSingleton<IKeyStore, MauiKeyStore>();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IFriendsService, FriendsService>();
        builder.Services.AddSingleton<IFriendCallsService, FriendCallsService>();
        builder.Services.AddSingleton<FriendCallsUiCoordinator>();
        builder.Services.AddSingleton<ICallsService, CallsService>();
        builder.Services.AddSingleton<IAudioService, AudioService>();
        
        builder.Services.AddSingleton<IStunClient, MainServerStunClient>();
        builder.Services.AddSingleton<UdpUnifiedManager>();
        
        // register DI MonkeySpeakClient
        builder.Services.AddSingleton<IMonkeySpeakClient>(sp => new MonkeySpeakClient(
            sp.GetRequiredService<IConnectionService>(),
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IFriendsService>(),
            sp.GetRequiredService<ICallsService>(),
            sp.GetRequiredService<IAudioService>()
        ));

        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            fonts.AddFont("Midami-Normal.ttf", "Midami");
            fonts.AddFont("JetBrainsMonoNL-Regular.ttf", "JetBrainsMono");
        }).ConfigureMauiHandlers(handlers =>
        {
#if WINDOWS
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, view) =>
            {
                if (view is not Microsoft.Maui.Controls.Entry entry)
                    return;

                var hasCustomBg = entry.BackgroundColor != null
                    && entry.BackgroundColor != Colors.Transparent
                    && entry.BackgroundColor != KnownColor.Default;

                var textBox = handler.PlatformView;

                if (hasCustomBg)
                {
                    textBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                    textBox.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(10);
                    textBox.Resources["TextControlBorderBrush"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    textBox.Resources["TextControlBorderBrushPointerOver"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    textBox.Resources["TextControlBorderBrushFocused"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    textBox.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
                }
                else
                {
                    textBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                    textBox.Padding = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 4);
                    var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    textBox.Background = transparent;
                    textBox.Resources["TextControlBorderBrush"] = transparent;
                    textBox.Resources["TextControlBorderBrushPointerOver"] = transparent;
                    textBox.Resources["TextControlBorderBrushFocused"] = transparent;
                    textBox.Resources["TextControlBorderBrushDisabled"] = transparent;
                    textBox.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
                }
            });
#endif
        });
#if DEBUG
        builder.Logging.AddDebug();

        Core.Logger.MinimumLevel = Core.Logger.LogLevel.Debug;
        Core.Logger.Sink = (level, message, ex) =>
        {
            var ts = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
            var tid = Environment.CurrentManagedThreadId;

            var line = $"[{ts}] [T{tid}] [{level}] {message}";
            if (ex is not null)
                line += $" | {ex.GetType().Name}: {ex.Message}";

            System.Diagnostics.Debug.WriteLine(line);
            Console.WriteLine(line);
        };
#endif

        return builder.Build();
    }
}