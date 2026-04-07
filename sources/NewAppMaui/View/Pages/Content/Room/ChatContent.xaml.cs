using Core.Application.Calls.Networking;
using Core.Public.Services;
using NewAppMaui.View.Components;

namespace NewAppMaui.View.Pages.Content;

public partial class ChatContent : ContentView
{
    private readonly ICallsService _calls;
    private readonly IUserSettingsService _settings;
    private readonly List<ChatMessage> _messages = new();

    public ChatContent()
    {
        var services = ((App)Application.Current!).Services;
        _calls = services.GetRequiredService<ICallsService>();
        _settings = services.GetRequiredService<IUserSettingsService>();
        InitializeComponent();
    }

    public void Activate()
    {
        _calls.ChatMessageReceived += OnChatMessageReceived;
    }

    public void Deactivate()
    {
        _calls.ChatMessageReceived -= OnChatMessageReceived;
    }

    private void OnChatMessageReceived(string interlocutorId, ChatMessage msg)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _messages.Add(msg);
            AppendMessageBubble(msg);
            ScrollToBottom();
        });
    }

    private void OnSendClicked(object? sender, EventArgs e)
    {
        var text = MessageEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _calls.SendChatMessage(text);

        var localMsg = new ChatMessage
        {
            SenderId = "",
            SenderName = _settings.DisplayName ?? "You",
            Text = text,
            Timestamp = DateTimeOffset.Now,
            IsLocal = true
        };
        _messages.Add(localMsg);
        AppendMessageBubble(localMsg);
        ScrollToBottom();
        MessageEntry.Text = string.Empty;
    }

    private string ResolveSenderName(ChatMessage msg)
    {
        if (!string.IsNullOrEmpty(msg.SenderName))
            return msg.SenderName;

        var it = _calls.Current?.Interlocutors?.FirstOrDefault(x => x.Id == msg.SenderId);
        if (!string.IsNullOrEmpty(it?.DisplayName))
            return it.DisplayName;

        return msg.SenderId.Length > 8 ? msg.SenderId[..8] : msg.SenderId;
    }

    private void AppendMessageBubble(ChatMessage msg)
    {
        var isLocal = msg.IsLocal;
        var displayName = isLocal ? msg.SenderName : ResolveSenderName(msg);

        var bubble = new HorizontalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = isLocal ? LayoutOptions.End : LayoutOptions.Start,
            Padding = new Thickness(4, 0)
        };

        if (!isLocal)
        {
            var avatar = new AvatarView();
            avatar.SetupInterlocutor(32, displayName, msg.SenderId);
            avatar.VerticalOptions = LayoutOptions.Start;
            bubble.Children.Add(avatar);
        }

        var content = new VerticalStackLayout { Spacing = 2, MaximumWidthRequest = 280 };

        if (!isLocal)
        {
            content.Children.Add(new Label
            {
                Text = displayName,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#9ca3af")
            });
        }

        var textBorder = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = isLocal ? Color.FromArgb("#14251d") : Color.FromArgb("#333333"),
            Padding = new Thickness(12, 8)
        };
        textBorder.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 };
        textBorder.Content = new Label
        {
            Text = msg.Text,
            FontSize = 14,
            TextColor = Colors.White,
            LineBreakMode = LineBreakMode.WordWrap
        };
        content.Children.Add(textBorder);

        content.Children.Add(new Label
        {
            Text = msg.Timestamp.ToLocalTime().ToString("HH:mm"),
            FontSize = 10,
            TextColor = Color.FromArgb("#888888"),
            HorizontalTextAlignment = isLocal ? TextAlignment.End : TextAlignment.Start
        });

        bubble.Children.Add(content);
        MessagesPanel.Children.Add(bubble);
    }

    private async void ScrollToBottom()
    {
        await Task.Delay(50);
        await MessagesScroll.ScrollToAsync(0, MessagesPanel.Height, true);
    }
}
