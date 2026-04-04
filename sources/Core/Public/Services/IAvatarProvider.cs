namespace Core.Public.Services;

public interface IAvatarProvider
{
    byte[]? GetOwnAvatarBytes();
    byte[]? GetOwnAvatarHash();
    byte[]? GetCachedHash(string interlocutorId);
    string? GetCachedAvatarPath(string interlocutorId);
    void SaveRemoteAvatar(string interlocutorId, byte[] jpegData, byte[] hash);
}
