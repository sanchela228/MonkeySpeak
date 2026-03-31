namespace Core.Websockets.Messages.AuthCall;

public class RegisterKey : IMessage
{
    public string Username { get; set; } = string.Empty;
    public string PublicKeyEd25519Base64 { get; set; } = string.Empty;
    public string PublicKeyX25519Base64 { get; set; } = string.Empty;
    public string ProofSignature { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
