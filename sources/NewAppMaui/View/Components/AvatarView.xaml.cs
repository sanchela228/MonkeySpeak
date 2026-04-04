using Microsoft.Maui.Controls.Shapes;
using NewAppMaui.Services;

namespace NewAppMaui.View.Components;

public partial class AvatarView : ContentView
{
    private readonly AvatarCacheService _avatarCache;
    private string? _userId;
    private string? _interlocutorId;

    public AvatarView()
    {
        _avatarCache = ((App)Application.Current!).Services.GetRequiredService<AvatarCacheService>();
        _avatarCache.UserAvatarUpdated += OnUserAvatarUpdated;
        InitializeComponent();
    }

    public void Setup(double size, string? name, string? userId)
    {
        _userId = userId;
        _interlocutorId = null;
        ApplySize(size);
        InitialsLabel.Text = GetInitials(name);
        LoadAvatar();
    }

    public void SetupInterlocutor(double size, string? name, string interlocutorId)
    {
        _interlocutorId = interlocutorId;
        _userId = null;
        ApplySize(size);
        InitialsLabel.Text = GetInitials(name);
        LoadAvatar();
    }

    private void ApplySize(double size)
    {
        var radius = size / 2.0;

        Root.WidthRequest = size;
        Root.HeightRequest = size;

        InitialsBorder.WidthRequest = size;
        InitialsBorder.HeightRequest = size;
        InitialsBorder.StrokeShape = new RoundRectangle { CornerRadius = (float)radius };

        InitialsLabel.FontSize = size * 0.35;

        AvatarImage.WidthRequest = size;
        AvatarImage.HeightRequest = size;
        AvatarImage.Clip = new EllipseGeometry
        {
            Center = new Point(radius, radius),
            RadiusX = radius,
            RadiusY = radius
        };
    }

    private void OnUserAvatarUpdated(string userId)
    {
        if (_userId != userId) return;
        MainThread.BeginInvokeOnMainThread(LoadAvatar);
    }

    public void Refresh()
    {
        LoadAvatar();
    }

    private void LoadAvatar()
    {
        string? path = null;

        if (_userId is not null)
            path = _avatarCache.GetAvatarPathForUser(_userId);
        else if (_interlocutorId is not null)
            path = _avatarCache.GetCachedAvatarPath(_interlocutorId);

        if (path != null)
        {
            AvatarImage.Source = ImageSource.FromStream(() => File.OpenRead(path));
            AvatarImage.IsVisible = true;
            InitialsBorder.IsVisible = false;
        }
        else
        {
            AvatarImage.IsVisible = false;
            InitialsBorder.IsVisible = true;
        }
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "??";
        return name.Length >= 2
            ? name[..2].ToUpperInvariant()
            : name.ToUpperInvariant();
    }
}
