namespace Core.Websockets.Messages.AuthCall;

public class ChangeUsername : IMessage
{
    public string NewUsername { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class UsernameChanged : IMessage
{
    public string NewUsername { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
