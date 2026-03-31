namespace Core.Domain.Friends;

public class Friend
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public string LastSeenAt { get; set; } = string.Empty;
}