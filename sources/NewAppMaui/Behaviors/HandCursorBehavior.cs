using MauiView = Microsoft.Maui.Controls.View;

namespace NewAppMaui.Behaviors;

public class HandCursorBehavior : Behavior<MauiView>
{
    protected override void OnAttachedTo(MauiView bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.HandlerChanged += OnHandlerChanged;
    }

    protected override void OnDetachingFrom(MauiView bindable)
    {
        bindable.HandlerChanged -= OnHandlerChanged;
        base.OnDetachingFrom(bindable);
    }

    static void OnHandlerChanged(object? sender, EventArgs e)
    {
        HandCursor.Apply(sender as MauiView);
    }
}

public static class HandCursor
{
    public static readonly BindableProperty EnabledProperty =
        BindableProperty.CreateAttached("Enabled", typeof(bool), typeof(HandCursor), false,
            propertyChanged: OnEnabledChanged);

    public static bool GetEnabled(BindableObject view) => (bool)view.GetValue(EnabledProperty);
    public static void SetEnabled(BindableObject view, bool value) => view.SetValue(EnabledProperty, value);

    static void OnEnabledChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MauiView view && (bool)newValue)
            view.HandlerChanged += (_, _) => Apply(view);
    }

    internal static void Apply(MauiView? view)
    {
#if WINDOWS
        if (view?.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement element)
            return;

        var hand = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
        var arrow = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);

        element.PointerEntered += (_, _) => element.ChangeCursor(hand);
        element.PointerExited += (_, _) => element.ChangeCursor(arrow);
#endif
    }
}

#if WINDOWS
static class WinUICursorExtensions
{
    public static void ChangeCursor(this Microsoft.UI.Xaml.UIElement element, Microsoft.UI.Input.InputCursor cursor)
    {
        var type = typeof(Microsoft.UI.Xaml.UIElement);
        var prop = type.GetProperty("ProtectedCursor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(element, cursor);
    }
}
#endif
