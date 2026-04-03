namespace NewAppMaui.View.Layout;

public partial class SidebarMenuItem : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(SidebarMenuItem), "",
            propertyChanged: (b, _, v) => ((SidebarMenuItem)b).TextLabel.Text = (string)v);

    public static readonly BindableProperty IconSourceProperty =
        BindableProperty.Create(nameof(IconSource), typeof(string), typeof(SidebarMenuItem), null,
            propertyChanged: (b, _, v) =>
            {
                var item = (SidebarMenuItem)b;
                var src = (string?)v;
                item._iconSource = src;
                item.IconImage.Source = src;
                item.IconImage.IsVisible = !string.IsNullOrEmpty(src);
            });

    public static readonly BindableProperty TextColorNormalProperty =
        BindableProperty.Create(nameof(TextColorNormal), typeof(Color), typeof(SidebarMenuItem), Colors.White,
            propertyChanged: (b, _, _) => ((SidebarMenuItem)b).ApplyState());

    public static readonly BindableProperty TextColorActiveProperty =
        BindableProperty.Create(nameof(TextColorActive), typeof(Color), typeof(SidebarMenuItem), null);

    public static readonly BindableProperty IconSourceActiveProperty =
        BindableProperty.Create(nameof(IconSourceActive), typeof(string), typeof(SidebarMenuItem), null);

    public static readonly BindableProperty ActiveBackgroundProperty =
        BindableProperty.Create(nameof(ActiveBackground), typeof(Color), typeof(SidebarMenuItem), Color.FromArgb("#1a1a1a"));

    public static readonly BindableProperty HoverBackgroundProperty =
        BindableProperty.Create(nameof(HoverBackground), typeof(Color), typeof(SidebarMenuItem), Color.FromArgb("#1a1a1a"),
            propertyChanged: (b, _, v) =>
            {
                var item = (SidebarMenuItem)b;
                var vsgs = VisualStateManager.GetVisualStateGroups(item.Container);
                if (vsgs.Count > 0 && vsgs[0].States.Count > 1)
                {
                    var hoverState = vsgs[0].States[1];
                    if (hoverState.Setters.Count > 0)
                        hoverState.Setters[0].Value = v;
                }
            });

    public static readonly BindableProperty IsActiveProperty =
        BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(SidebarMenuItem), false,
            propertyChanged: (b, _, _) => ((SidebarMenuItem)b).ApplyState());

    private string? _iconSource;

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? IconSource { get => (string?)GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }
    public Color TextColorNormal { get => (Color)GetValue(TextColorNormalProperty); set => SetValue(TextColorNormalProperty, value); }
    public Color? TextColorActive { get => (Color?)GetValue(TextColorActiveProperty); set => SetValue(TextColorActiveProperty, value); }
    public string? IconSourceActive { get => (string?)GetValue(IconSourceActiveProperty); set => SetValue(IconSourceActiveProperty, value); }
    public Color ActiveBackground { get => (Color)GetValue(ActiveBackgroundProperty); set => SetValue(ActiveBackgroundProperty, value); }
    public Color HoverBackground { get => (Color)GetValue(HoverBackgroundProperty); set => SetValue(HoverBackgroundProperty, value); }
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    public event EventHandler? Clicked;

    public SidebarMenuItem()
    {
        InitializeComponent();
    }

    private void ApplyState()
    {
        if (IsActive)
        {
            Container.BackgroundColor = ActiveBackground;
            TextLabel.TextColor = TextColorActive ?? TextColorNormal;
            if (!string.IsNullOrEmpty(IconSourceActive))
                IconImage.Source = IconSourceActive;
        }
        else
        {
            Container.BackgroundColor = Colors.Transparent;
            TextLabel.TextColor = TextColorNormal;
            if (!string.IsNullOrEmpty(_iconSource))
                IconImage.Source = _iconSource;
        }
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        Clicked?.Invoke(this, EventArgs.Empty);
    }
}
