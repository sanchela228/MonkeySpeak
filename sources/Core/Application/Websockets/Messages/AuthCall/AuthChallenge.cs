namespace Core.Websockets.Messages.AuthCall;

public class AuthChallenge : IMessage
{
    public string Nonce { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
