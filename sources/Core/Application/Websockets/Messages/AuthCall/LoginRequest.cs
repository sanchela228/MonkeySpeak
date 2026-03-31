namespace Core.Websockets.Messages.AuthCall;

public class LoginRequest : IMessage
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
