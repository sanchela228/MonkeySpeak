namespace Core.Websockets.Messages.AuthCall;

public class ErrorRegistration : IMessage
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
